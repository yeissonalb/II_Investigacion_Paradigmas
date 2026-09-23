using System.Text.Json.Serialization;
using ServiceA.Domain;

namespace ServiceA.Models;

public record BattleStarted(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("heroName")] string HeroName,
    [property: JsonPropertyName("heroMaxHp")] int HeroMaxHp,
    [property: JsonPropertyName("enemyName")] string EnemyName,
    [property: JsonPropertyName("enemyMaxHp")] int EnemyMaxHp,
    [property: JsonPropertyName("startedAt")] DateTime StartedAt,
    [property: JsonPropertyName("heroPokemonId")] int? HeroPokemonId = null,
    [property: JsonPropertyName("heroSprite")] string? HeroSprite = null,
    [property: JsonPropertyName("enemyPokemonId")] int? EnemyPokemonId = null,
    [property: JsonPropertyName("enemySprite")] string? EnemySprite = null,
    [property: JsonPropertyName("heroTypes")] List<string>? HeroTypes = null,
    [property: JsonPropertyName("heroAttack")] int? HeroAttack = null,
    [property: JsonPropertyName("heroDefense")] int? HeroDefense = null,
    [property: JsonPropertyName("heroSpecialAttack")] int? HeroSpecialAttack = null,
    [property: JsonPropertyName("heroSpecialDefense")] int? HeroSpecialDefense = null,
    [property: JsonPropertyName("heroSpeed")] int? HeroSpeed = null,
    [property: JsonPropertyName("heroMoves")] List<PokemonMove>? HeroMoves = null,
    [property: JsonPropertyName("enemyTypes")] List<string>? EnemyTypes = null,
    [property: JsonPropertyName("enemyAttack")] int? EnemyAttack = null,
    [property: JsonPropertyName("enemyDefense")] int? EnemyDefense = null,
    [property: JsonPropertyName("enemySpecialAttack")] int? EnemySpecialAttack = null,
    [property: JsonPropertyName("enemySpecialDefense")] int? EnemySpecialDefense = null,
    [property: JsonPropertyName("enemySpeed")] int? EnemySpeed = null,
    [property: JsonPropertyName("enemyMoves")] List<PokemonMove>? EnemyMoves = null
);

public record AttackPerformed(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("attacker")] string Attacker,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("damage")] int Damage,
    [property: JsonPropertyName("targetRemainingHp")] int TargetRemainingHp,
    [property: JsonPropertyName("isCritical")] bool IsCritical,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("moveName")] string? MoveName = null,
    [property: JsonPropertyName("moveType")] string? MoveType = null,
    [property: JsonPropertyName("damageClass")] string? DamageClass = null,
    [property: JsonPropertyName("basePower")] int? BasePower = null,
    [property: JsonPropertyName("effectiveness")] double? Effectiveness = null,
    [property: JsonPropertyName("stab")] double? Stab = null,
    [property: JsonPropertyName("hit")] bool? Hit = null
);

public record HealUsed(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("healAmount")] int HealAmount,
    [property: JsonPropertyName("targetRemainingHp")] int TargetRemainingHp,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
);

public record StartBattleRequest(
    string? BattleId,
    string? HeroName,
    int? HeroHp,
    string? EnemyName,
    int? EnemyHp,
    int? HeroPokemonId = null,
    string? HeroSprite = null,
    int? EnemyPokemonId = null,
    string? EnemySprite = null,
    List<string>? HeroTypes = null,
    int? HeroAttack = null,
    int? HeroDefense = null,
    int? HeroSpecialAttack = null,
    int? HeroSpecialDefense = null,
    int? HeroSpeed = null,
    List<PokemonMove>? HeroMoves = null,
    List<string>? EnemyTypes = null,
    int? EnemyAttack = null,
    int? EnemyDefense = null,
    int? EnemySpecialAttack = null,
    int? EnemySpecialDefense = null,
    int? EnemySpeed = null,
    List<PokemonMove>? EnemyMoves = null
);

public record AttackRequest(
    string? Attacker,
    int? Damage,
    string? MoveName = null
);

public record HealRequest(
    string? Target,
    int? Amount
);
