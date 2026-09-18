using System.Text.Json;
using ServiceA.Models;

namespace ServiceA.Services;

public class BattleAggregate
{
    public string BattleId { get; private set; } = string.Empty;
    public bool Exists { get; private set; } = false;

    public string HeroName { get; private set; } = "Héroe";
    public int HeroMaxHp { get; private set; } = 100;
    public int HeroCurrentHp { get; private set; } = 100;

    public string EnemyName { get; private set; } = "Enemigo";
    public int EnemyMaxHp { get; private set; } = 100;
    public int EnemyCurrentHp { get; private set; } = 100;

    public bool IsFinished => Exists && (HeroCurrentHp <= 0 || EnemyCurrentHp <= 0);
    public string? Winner => !IsFinished ? null : (HeroCurrentHp > 0 ? HeroName : EnemyName);
    public string? Defeated => !IsFinished ? null : (HeroCurrentHp <= 0 ? HeroName : EnemyName);

    public void Apply(string eventType, string json)
    {
        switch (eventType)
        {
            case nameof(BattleStarted):
                var start = JsonSerializer.Deserialize<BattleStarted>(json);
                if (start != null)
                {
                    BattleId = start.BattleId;
                    HeroName = start.HeroName;
                    HeroMaxHp = start.HeroMaxHp;
                    HeroCurrentHp = start.HeroMaxHp;
                    EnemyName = start.EnemyName;
                    EnemyMaxHp = start.EnemyMaxHp;
                    EnemyCurrentHp = start.EnemyMaxHp;
                    Exists = true;
                }
                break;

            case nameof(AttackPerformed):
                var attack = JsonSerializer.Deserialize<AttackPerformed>(json);
                if (attack != null)
                {
                    if (attack.Target.Equals("Enemy", StringComparison.OrdinalIgnoreCase) || attack.Target.Equals(EnemyName, StringComparison.OrdinalIgnoreCase))
                    {
                        EnemyCurrentHp = Math.Max(0, attack.TargetRemainingHp);
                    }
                    else
                    {
                        HeroCurrentHp = Math.Max(0, attack.TargetRemainingHp);
                    }
                }
                break;

            case nameof(HealUsed):
                var heal = JsonSerializer.Deserialize<HealUsed>(json);
                if (heal != null)
                {
                    if (heal.Target.Equals("Enemy", StringComparison.OrdinalIgnoreCase) || heal.Target.Equals(EnemyName, StringComparison.OrdinalIgnoreCase))
                    {
                        EnemyCurrentHp = Math.Min(EnemyMaxHp, heal.TargetRemainingHp);
                    }
                    else
                    {
                        HeroCurrentHp = Math.Min(HeroMaxHp, heal.TargetRemainingHp);
                    }
                }
                break;
        }
    }

    public bool CanAttack(string attacker, string target, out string? error)
    {
        if (!Exists)
        {
            error = $"La batalla '{BattleId}' no existe o no ha sido iniciada.";
            return false;
        }

        if (IsFinished)
        {
            error = $"No se puede atacar. La batalla ya culminó. Ganador: '{Winner}', derrotado: '{Defeated}'.";
            return false;
        }

        error = null;
        return true;
    }

    public bool CanHeal(string target, out string? error)
    {
        if (!Exists)
        {
            error = $"La batalla '{BattleId}' no existe o no ha sido iniciada.";
            return false;
        }

        if (IsFinished)
        {
            error = $"No se puede curar. La batalla ya culminó. Ganador: '{Winner}', derrotado: '{Defeated}'.";
            return false;
        }

        bool isTargetHero = !target.Equals("Enemy", StringComparison.OrdinalIgnoreCase) && !target.Equals(EnemyName, StringComparison.OrdinalIgnoreCase);
        int currentHp = isTargetHero ? HeroCurrentHp : EnemyCurrentHp;
        int maxHp = isTargetHero ? HeroMaxHp : EnemyMaxHp;
        string name = isTargetHero ? HeroName : EnemyName;

        if (currentHp >= maxHp)
        {
            error = $"{name} ya tiene la vida al máximo ({currentHp}/{maxHp} HP). No requiere curación.";
            return false;
        }

        error = null;
        return true;
    }
}
