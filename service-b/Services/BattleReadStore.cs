using System.Collections.Concurrent;
using System.Text.Json;
using ServiceB.Models;

namespace ServiceB.Services;

public class BattleReadStore
{
    private readonly ConcurrentDictionary<string, BattleReadModel> _byStream = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _idToStream = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byStream.Count;

    public static string GetStreamName(string battleId)
    {
        var cleanId = battleId.StartsWith("battle-", StringComparison.OrdinalIgnoreCase)
            ? battleId["battle-".Length..]
            : battleId;
        return $"battle-{cleanId}";
    }

    public IReadOnlyList<BattleSummaryDto> GetAll()
    {
        return _byStream.Values
            .Select(SnapshotSummary)
            .OrderByDescending(b => b.UpdatedAt)
            .ToList();
    }

    public BattleDetailDto? GetDetail(string battleId)
    {
        var model = Find(battleId);
        return model is null ? null : SnapshotDetail(model);
    }

    public BattleStatsDto? GetStats(string battleId)
    {
        var model = Find(battleId);
        return model is null ? null : SnapshotStats(model);
    }

    public BattleHistoryDto? GetHistory(string battleId)
    {
        var model = Find(battleId);
        return model is null ? null : SnapshotHistory(model);
    }

    public void Apply(string streamId, string eventType, string json, long eventNumber, DateTime createdUtc)
    {
        switch (eventType)
        {
            case nameof(BattleStarted):
                ApplyBattleStarted(streamId, json, eventNumber, createdUtc);
                break;
            case nameof(AttackPerformed):
                ApplyAttack(streamId, json, eventNumber, createdUtc);
                break;
            case nameof(HealUsed):
                ApplyHeal(streamId, json, eventNumber, createdUtc);
                break;
        }
    }

    private void ApplyBattleStarted(string streamId, string json, long eventNumber, DateTime createdUtc)
    {
        var started = JsonSerializer.Deserialize<BattleStarted>(json);
        if (started is null)
        {
            return;
        }

        var model = _byStream.GetOrAdd(streamId, _ => new BattleReadModel());
        lock (model)
        {
            model.BattleId = started.BattleId;
            model.Stream = streamId;
            model.Hero = new FighterView
            {
                Name = started.HeroName,
                Hp = started.HeroMaxHp,
                MaxHp = started.HeroMaxHp
            };
            model.Enemy = new FighterView
            {
                Name = started.EnemyName,
                Hp = started.EnemyMaxHp,
                MaxHp = started.EnemyMaxHp
            };
            model.IsFinished = false;
            model.Winner = null;
            model.Defeated = null;
            model.StartedAt = started.StartedAt == default ? createdUtc : started.StartedAt;
            model.UpdatedAt = model.StartedAt;
            model.Stats = new BattleStatsView();
            model.History.Clear();
            model.History.Add(new BattleHistoryItem
            {
                EventNumber = eventNumber,
                EventType = nameof(BattleStarted),
                Timestamp = model.StartedAt,
                Description = $"{started.HeroName} ({started.HeroMaxHp} HP) vs {started.EnemyName} ({started.EnemyMaxHp} HP)"
            });
        }

        Index(started.BattleId, streamId);
    }

    private void ApplyAttack(string streamId, string json, long eventNumber, DateTime createdUtc)
    {
        var attack = JsonSerializer.Deserialize<AttackPerformed>(json);
        if (attack is null)
        {
            return;
        }

        var model = GetOrCreatePending(streamId, attack.BattleId);
        lock (model)
        {
            var timestamp = attack.Timestamp == default ? createdUtc : attack.Timestamp;
            var isTargetEnemy = IsEnemy(model, attack.Target);
            var isAttackerHero = IsHero(model, attack.Attacker);

            if (isTargetEnemy)
            {
                model.Enemy.Hp = Math.Max(0, attack.TargetRemainingHp);
            }
            else
            {
                model.Hero.Hp = Math.Max(0, attack.TargetRemainingHp);
            }

            model.Stats.Turns++;
            model.Stats.Actions++;
            model.Stats.TotalDamage += attack.Damage;
            if (isAttackerHero)
            {
                model.Stats.HeroDamageDealt += attack.Damage;
            }
            else
            {
                model.Stats.EnemyDamageDealt += attack.Damage;
            }

            if (attack.IsCritical)
            {
                model.Stats.CriticalHits++;
            }

            RefreshOutcome(model);
            model.UpdatedAt = timestamp;
            model.History.Add(new BattleHistoryItem
            {
                EventNumber = eventNumber,
                EventType = nameof(AttackPerformed),
                Timestamp = timestamp,
                Description = $"{ResolveName(model, attack.Attacker)} hizo {attack.Damage} de daño a {ResolveName(model, attack.Target)}" +
                              $" (HP restante: {attack.TargetRemainingHp}){(attack.IsCritical ? " [CRÍTICO]" : string.Empty)}"
            });
        }
    }

    private void ApplyHeal(string streamId, string json, long eventNumber, DateTime createdUtc)
    {
        var heal = JsonSerializer.Deserialize<HealUsed>(json);
        if (heal is null)
        {
            return;
        }

        var model = GetOrCreatePending(streamId, heal.BattleId);
        lock (model)
        {
            var timestamp = heal.Timestamp == default ? createdUtc : heal.Timestamp;
            var isTargetEnemy = IsEnemy(model, heal.Target);

            if (isTargetEnemy)
            {
                model.Enemy.Hp = Math.Min(model.Enemy.MaxHp, heal.TargetRemainingHp);
                model.Stats.EnemyHealed += heal.HealAmount;
            }
            else
            {
                model.Hero.Hp = Math.Min(model.Hero.MaxHp, heal.TargetRemainingHp);
                model.Stats.HeroHealed += heal.HealAmount;
            }

            model.Stats.Actions++;
            model.Stats.TotalHealed += heal.HealAmount;
            RefreshOutcome(model);
            model.UpdatedAt = timestamp;
            model.History.Add(new BattleHistoryItem
            {
                EventNumber = eventNumber,
                EventType = nameof(HealUsed),
                Timestamp = timestamp,
                Description = $"{ResolveName(model, heal.Target)} se curó +{heal.HealAmount} HP (HP actual: {heal.TargetRemainingHp})"
            });
        }
    }

    private BattleReadModel GetOrCreatePending(string streamId, string battleId)
    {
        var model = _byStream.GetOrAdd(streamId, _ => new BattleReadModel
        {
            BattleId = battleId,
            Stream = streamId
        });
        Index(battleId, streamId);
        return model;
    }

    private void Index(string battleId, string streamId)
    {
        if (!string.IsNullOrWhiteSpace(battleId))
        {
            _idToStream[battleId] = streamId;
        }

        _idToStream[streamId] = streamId;
    }

    private BattleReadModel? Find(string battleId)
    {
        if (_idToStream.TryGetValue(battleId, out var stream) && _byStream.TryGetValue(stream, out var mapped))
        {
            return mapped;
        }

        var streamName = GetStreamName(battleId);
        if (_byStream.TryGetValue(streamName, out var byStream))
        {
            return byStream;
        }

        return _byStream.TryGetValue(battleId, out var direct) ? direct : null;
    }

    private static void RefreshOutcome(BattleReadModel model)
    {
        model.IsFinished = model.Hero.Hp <= 0 || model.Enemy.Hp <= 0;
        if (!model.IsFinished)
        {
            model.Winner = null;
            model.Defeated = null;
            return;
        }

        model.Winner = model.Hero.Hp > 0 ? model.Hero.Name : model.Enemy.Name;
        model.Defeated = model.Hero.Hp <= 0 ? model.Hero.Name : model.Enemy.Name;
    }

    private static bool IsHero(BattleReadModel model, string name) =>
        name.Equals("Hero", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(model.Hero.Name, StringComparison.OrdinalIgnoreCase);

    private static bool IsEnemy(BattleReadModel model, string name) =>
        name.Equals("Enemy", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(model.Enemy.Name, StringComparison.OrdinalIgnoreCase);

    private static string ResolveName(BattleReadModel model, string name)
    {
        if (IsHero(model, name) && !string.IsNullOrWhiteSpace(model.Hero.Name))
        {
            return model.Hero.Name;
        }

        if (IsEnemy(model, name) && !string.IsNullOrWhiteSpace(model.Enemy.Name))
        {
            return model.Enemy.Name;
        }

        return name;
    }

    private static BattleSummaryDto SnapshotSummary(BattleReadModel model)
    {
        lock (model)
        {
            return new BattleSummaryDto
            {
                BattleId = model.BattleId,
                Stream = model.Stream,
                Hero = CopyFighter(model.Hero),
                Enemy = CopyFighter(model.Enemy),
                IsFinished = model.IsFinished,
                Winner = model.Winner,
                StartedAt = model.StartedAt,
                UpdatedAt = model.UpdatedAt
            };
        }
    }

    private static BattleDetailDto SnapshotDetail(BattleReadModel model)
    {
        lock (model)
        {
            return new BattleDetailDto
            {
                BattleId = model.BattleId,
                Stream = model.Stream,
                Hero = CopyFighter(model.Hero),
                Enemy = CopyFighter(model.Enemy),
                IsFinished = model.IsFinished,
                Winner = model.Winner,
                Defeated = model.Defeated,
                StartedAt = model.StartedAt,
                UpdatedAt = model.UpdatedAt,
                Stats = CopyStats(model.Stats)
            };
        }
    }

    private static BattleStatsDto SnapshotStats(BattleReadModel model)
    {
        lock (model)
        {
            return new BattleStatsDto
            {
                BattleId = model.BattleId,
                IsFinished = model.IsFinished,
                Winner = model.Winner,
                Hero = CopyFighter(model.Hero),
                Enemy = CopyFighter(model.Enemy),
                Stats = CopyStats(model.Stats)
            };
        }
    }

    private static BattleHistoryDto SnapshotHistory(BattleReadModel model)
    {
        lock (model)
        {
            return new BattleHistoryDto
            {
                BattleId = model.BattleId,
                Stream = model.Stream,
                History = model.History
                    .Select(item => new BattleHistoryItem
                    {
                        EventNumber = item.EventNumber,
                        EventType = item.EventType,
                        Timestamp = item.Timestamp,
                        Description = item.Description
                    })
                    .ToList()
            };
        }
    }

    private static FighterView CopyFighter(FighterView fighter) => new()
    {
        Name = fighter.Name,
        Hp = fighter.Hp,
        MaxHp = fighter.MaxHp
    };

    private static BattleStatsView CopyStats(BattleStatsView stats) => new()
    {
        Turns = stats.Turns,
        Actions = stats.Actions,
        TotalDamage = stats.TotalDamage,
        HeroDamageDealt = stats.HeroDamageDealt,
        EnemyDamageDealt = stats.EnemyDamageDealt,
        TotalHealed = stats.TotalHealed,
        HeroHealed = stats.HeroHealed,
        EnemyHealed = stats.EnemyHealed,
        CriticalHits = stats.CriticalHits
    };
}
