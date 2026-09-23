namespace ServiceA.Domain;

public static class TypeChart
{
    public const double StabBonus = 1.5;

    private static readonly Dictionary<string, double> Chart = Build();

    public static double Against(string moveType, IReadOnlyList<string>? defenderTypes)
    {
        if (defenderTypes is null || defenderTypes.Count == 0)
        {
            return 1;
        }

        var total = 1d;
        foreach (var defenderType in defenderTypes)
        {
            total *= Single(moveType, defenderType);
        }

        return total;
    }

    public static double StabFor(string moveType, IReadOnlyList<string>? attackerTypes)
    {
        if (attackerTypes is null)
        {
            return 1;
        }

        return attackerTypes.Any(type => type.Equals(moveType, StringComparison.OrdinalIgnoreCase))
            ? StabBonus
            : 1;
    }

    public static double Single(string moveType, string defenderType)
    {
        var key = Key(moveType, defenderType);
        return Chart.TryGetValue(key, out var multiplier) ? multiplier : 1;
    }

    private static Dictionary<string, double> Build()
    {
        var chart = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        void Add(string attack, params (string Defense, double Multiplier)[] pairs)
        {
            foreach (var (defense, multiplier) in pairs)
            {
                chart[Key(attack, defense)] = multiplier;
            }
        }

        Add("normal", ("rock", 0.5), ("ghost", 0), ("steel", 0.5));
        Add("fire", ("fire", 0.5), ("water", 0.5), ("grass", 2), ("ice", 2), ("bug", 2), ("rock", 0.5), ("dragon", 0.5), ("steel", 2));
        Add("water", ("fire", 2), ("water", 0.5), ("grass", 0.5), ("ground", 2), ("rock", 2), ("dragon", 0.5));
        Add("electric", ("water", 2), ("electric", 0.5), ("grass", 0.5), ("ground", 0), ("flying", 2), ("dragon", 0.5));
        Add("grass", ("fire", 0.5), ("water", 2), ("grass", 0.5), ("poison", 0.5), ("ground", 2), ("flying", 0.5), ("bug", 0.5), ("rock", 2), ("dragon", 0.5), ("steel", 0.5));
        Add("ice", ("fire", 0.5), ("water", 0.5), ("grass", 2), ("ice", 0.5), ("ground", 2), ("flying", 2), ("dragon", 2), ("steel", 0.5));
        Add("fighting", ("normal", 2), ("ice", 2), ("poison", 0.5), ("flying", 0.5), ("psychic", 0.5), ("bug", 0.5), ("rock", 2), ("ghost", 0), ("dark", 2), ("steel", 2), ("fairy", 0.5));
        Add("poison", ("grass", 2), ("poison", 0.5), ("ground", 0.5), ("rock", 0.5), ("ghost", 0.5), ("steel", 0), ("fairy", 2));
        Add("ground", ("fire", 2), ("electric", 2), ("grass", 0.5), ("poison", 2), ("flying", 0), ("bug", 0.5), ("rock", 2), ("steel", 2));
        Add("flying", ("electric", 0.5), ("grass", 2), ("fighting", 2), ("bug", 2), ("rock", 0.5), ("steel", 0.5));
        Add("psychic", ("fighting", 2), ("poison", 2), ("psychic", 0.5), ("dark", 0), ("steel", 0.5));
        Add("bug", ("fire", 0.5), ("grass", 2), ("fighting", 0.5), ("poison", 0.5), ("flying", 0.5), ("psychic", 2), ("ghost", 0.5), ("dark", 2), ("steel", 0.5), ("fairy", 0.5));
        Add("rock", ("fire", 2), ("ice", 2), ("fighting", 0.5), ("ground", 0.5), ("flying", 2), ("bug", 2), ("steel", 0.5));
        Add("ghost", ("normal", 0), ("psychic", 2), ("ghost", 2), ("dark", 0.5));
        Add("dragon", ("dragon", 2), ("steel", 0.5), ("fairy", 0));
        Add("dark", ("fighting", 0.5), ("psychic", 2), ("ghost", 2), ("dark", 0.5), ("fairy", 0.5));
        Add("steel", ("fire", 0.5), ("water", 0.5), ("electric", 0.5), ("ice", 2), ("rock", 2), ("steel", 0.5), ("fairy", 2));
        Add("fairy", ("fire", 0.5), ("fighting", 2), ("poison", 0.5), ("dragon", 2), ("dark", 2), ("steel", 0.5));
        return chart;
    }

    private static string Key(string attack, string defense) =>
        $"{attack.Trim().ToLowerInvariant()}|{defense.Trim().ToLowerInvariant()}";
}
