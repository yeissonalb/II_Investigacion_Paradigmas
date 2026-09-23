namespace ServiceA.Domain;

public readonly record struct Combatant(
    int Attack,
    int Defense,
    int SpecialAttack,
    int SpecialDefense,
    IReadOnlyList<string> Types);

public readonly record struct CombatRoll(int AccuracyRoll, bool Critical, double RandomFactor)
{
    public static CombatRoll Create(Random random) => new(
        random.Next(1, 101),
        random.Next(16) == 0,
        random.Next(85, 101) / 100d);
}

public readonly record struct MoveOutcome(
    int Damage,
    bool Hit,
    bool Critical,
    double Effectiveness,
    double Stab);

public static class DamageCalculator
{
    public const int AssumedLevel = 50;
    public const double CriticalMultiplier = 1.5;

    // Nivel fijo 50, porque este laboratorio no modela niveles.
    // base = floor(floor((2*nivel/5+2) * poder * ataque / defensa) / 50) + 2
    // final = base * STAB * efectividad * crítico * aleatorio(0.85–1.00)
    // Un fallo o una inmunidad dejan el daño en 0. Si pega y es efectivo, el mínimo es 1.
    public static int Calculate(
        int attackStat,
        int defenseStat,
        int power,
        double stab,
        double effectiveness,
        bool critical,
        double randomFactor,
        bool hit)
    {
        if (!hit || effectiveness <= 0 || power <= 0)
        {
            return 0;
        }

        attackStat = Math.Max(1, attackStat);
        defenseStat = Math.Max(1, defenseStat);
        randomFactor = Math.Clamp(randomFactor, 0.85, 1);
        var levelTerm = 2 * AssumedLevel / 5 + 2;
        var baseDamage = (levelTerm * power * attackStat / defenseStat) / 50 + 2;
        var multiplier = stab * effectiveness * (critical ? CriticalMultiplier : 1) * randomFactor;
        return Math.Max(1, (int)Math.Floor(baseDamage * multiplier));
    }
}

public static class MoveCombat
{
    public static MoveOutcome Resolve(Combatant attacker, Combatant defender, PokemonMove move, CombatRoll roll)
    {
        var effectiveness = TypeChart.Against(move.Type, defender.Types);
        var stab = TypeChart.StabFor(move.Type, attacker.Types);
        var hit = move.Accuracy is null || roll.AccuracyRoll <= move.Accuracy.Value;
        var physical = move.DamageClass.Equals("physical", StringComparison.OrdinalIgnoreCase);
        var attackStat = physical ? attacker.Attack : attacker.SpecialAttack;
        var defenseStat = physical ? defender.Defense : defender.SpecialDefense;
        var damage = DamageCalculator.Calculate(attackStat, defenseStat, move.Power, stab, effectiveness, roll.Critical, roll.RandomFactor, hit);
        var critical = roll.Critical && hit && effectiveness > 0 && damage > 0;
        return new MoveOutcome(damage, hit, critical, effectiveness, stab);
    }
}
