namespace ServiceB.Models;

public class FighterView
{
    public string Name { get; set; } = string.Empty;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int? PokemonId { get; set; }
    public string? Sprite { get; set; }
}

public class BattleStatsView
{
    public int Turns { get; set; }
    public int Actions { get; set; }
    public int TotalDamage { get; set; }
    public int HeroDamageDealt { get; set; }
    public int EnemyDamageDealt { get; set; }
    public int TotalHealed { get; set; }
    public int HeroHealed { get; set; }
    public int EnemyHealed { get; set; }
    public int CriticalHits { get; set; }
}

public class BattleHistoryItem
{
    public long EventNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class BattleReadModel
{
    public string BattleId { get; set; } = string.Empty;
    public string Stream { get; set; } = string.Empty;
    public FighterView Hero { get; set; } = new();
    public FighterView Enemy { get; set; } = new();
    public bool IsFinished { get; set; }
    public string? Winner { get; set; }
    public string? Defeated { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public BattleStatsView Stats { get; set; } = new();
    public List<BattleHistoryItem> History { get; } = [];
}

public class BattleSummaryDto
{
    public required string BattleId { get; init; }
    public required string Stream { get; init; }
    public required FighterView Hero { get; init; }
    public required FighterView Enemy { get; init; }
    public required bool IsFinished { get; init; }
    public string? Winner { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class BattleDetailDto
{
    public required string BattleId { get; init; }
    public required string Stream { get; init; }
    public required FighterView Hero { get; init; }
    public required FighterView Enemy { get; init; }
    public required bool IsFinished { get; init; }
    public string? Winner { get; init; }
    public string? Defeated { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required BattleStatsView Stats { get; init; }
}

public class BattleStatsDto
{
    public required string BattleId { get; init; }
    public required bool IsFinished { get; init; }
    public string? Winner { get; init; }
    public required FighterView Hero { get; init; }
    public required FighterView Enemy { get; init; }
    public required BattleStatsView Stats { get; init; }
}

public class BattleHistoryDto
{
    public required string BattleId { get; init; }
    public required string Stream { get; init; }
    public required IReadOnlyList<BattleHistoryItem> History { get; init; }
}
