using System.Text.Json.Serialization;

namespace ServiceB.Models;

public record BattleStarted(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("heroName")] string HeroName,
    [property: JsonPropertyName("heroMaxHp")] int HeroMaxHp,
    [property: JsonPropertyName("enemyName")] string EnemyName,
    [property: JsonPropertyName("enemyMaxHp")] int EnemyMaxHp,
    [property: JsonPropertyName("startedAt")] DateTime StartedAt
);

public record AttackPerformed(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("attacker")] string Attacker,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("damage")] int Damage,
    [property: JsonPropertyName("targetRemainingHp")] int TargetRemainingHp,
    [property: JsonPropertyName("isCritical")] bool IsCritical,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
);

public record HealUsed(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("healAmount")] int HealAmount,
    [property: JsonPropertyName("targetRemainingHp")] int TargetRemainingHp,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
);
