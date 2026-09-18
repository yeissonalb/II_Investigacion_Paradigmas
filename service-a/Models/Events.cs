using System.Text.Json.Serialization;

namespace ServiceA.Models;

// ==========================================
// EVENTOS DE DOMINIO (Contrato de Event Sourcing)
// ==========================================

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

// ==========================================
// DTOs DE ENTRADA HTTP (Comandos POST)
// ==========================================

public record StartBattleRequest(
    string? BattleId,
    string? HeroName,
    int? HeroHp,
    string? EnemyName,
    int? EnemyHp
);

public record AttackRequest(
    string? Attacker, // "Hero" o "Enemy" (por defecto "Hero")
    int? Damage       // Daño opcional, si no se envía se calcula automático (15-30)
);

public record HealRequest(
    string? Target,   // "Hero" o "Enemy" (por defecto "Hero")
    int? Amount       // Curación opcional, si no se envía se calcula automático (20)
);
