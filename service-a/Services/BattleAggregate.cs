using System.Text.Json;
using ServiceA.Domain;
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

    public int? HeroAttack { get; private set; }
    public int? HeroDefense { get; private set; }
    public int? HeroSpecialAttack { get; private set; }
    public int? HeroSpecialDefense { get; private set; }
    public int? HeroSpeed { get; private set; }
    public IReadOnlyList<string> HeroTypes { get; private set; } = [];
    public IReadOnlyList<PokemonMove> HeroMoves { get; private set; } = [];

    public int? EnemyAttack { get; private set; }
    public int? EnemyDefense { get; private set; }
    public int? EnemySpecialAttack { get; private set; }
    public int? EnemySpecialDefense { get; private set; }
    public int? EnemySpeed { get; private set; }
    public IReadOnlyList<string> EnemyTypes { get; private set; } = [];
    public IReadOnlyList<PokemonMove> EnemyMoves { get; private set; } = [];

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
                    HeroAttack = start.HeroAttack;
                    HeroDefense = start.HeroDefense;
                    HeroSpecialAttack = start.HeroSpecialAttack;
                    HeroSpecialDefense = start.HeroSpecialDefense;
                    HeroSpeed = start.HeroSpeed;
                    HeroTypes = start.HeroTypes ?? [];
                    HeroMoves = start.HeroMoves ?? [];
                    EnemyAttack = start.EnemyAttack;
                    EnemyDefense = start.EnemyDefense;
                    EnemySpecialAttack = start.EnemySpecialAttack;
                    EnemySpecialDefense = start.EnemySpecialDefense;
                    EnemySpeed = start.EnemySpeed;
                    EnemyTypes = start.EnemyTypes ?? [];
                    EnemyMoves = start.EnemyMoves ?? [];
                    Exists = true;
                }
                break;

            case nameof(AttackPerformed):
                var attack = JsonSerializer.Deserialize<AttackPerformed>(json);
                if (attack != null)
                {
                    // El replay aplica el HP persistido. No vuelve a calcular el daño.
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

    public bool HasMoves(bool isHero) => MovesOf(isHero).Count > 0;

    public bool TryGetMove(bool isHero, string moveName, out PokemonMove? move, out string? error)
    {
        var moves = MovesOf(isHero);
        if (moves.Count == 0)
        {
            move = null;
            error = "Este combate no tiene movimientos Pokémon.";
            return false;
        }

        move = moves.FirstOrDefault(candidate => candidate.Name.Equals(moveName, StringComparison.OrdinalIgnoreCase));
        if (move is null)
        {
            error = $"El movimiento '{moveName}' no pertenece a este Pokémon.";
            return false;
        }

        if (!HasCombatStats(isHero))
        {
            move = null;
            error = "Este combate no tiene las estadísticas necesarias para usar movimientos.";
            return false;
        }

        error = null;
        return true;
    }

    public Combatant AttackerProfile(bool isHero) => isHero ? HeroProfile() : EnemyProfile();

    public Combatant DefenderProfile(bool isHero) => isHero ? EnemyProfile() : HeroProfile();

    private IReadOnlyList<PokemonMove> MovesOf(bool isHero) => isHero ? HeroMoves : EnemyMoves;

    private bool HasCombatStats(bool isHero) => isHero
        ? HeroAttack is not null && HeroDefense is not null && HeroSpecialAttack is not null && HeroSpecialDefense is not null
        : EnemyAttack is not null && EnemyDefense is not null && EnemySpecialAttack is not null && EnemySpecialDefense is not null;

    private Combatant HeroProfile() => new(
        HeroAttack ?? 1,
        HeroDefense ?? 1,
        HeroSpecialAttack ?? 1,
        HeroSpecialDefense ?? 1,
        HeroTypes);

    private Combatant EnemyProfile() => new(
        EnemyAttack ?? 1,
        EnemyDefense ?? 1,
        EnemySpecialAttack ?? 1,
        EnemySpecialDefense ?? 1,
        EnemyTypes);
}
