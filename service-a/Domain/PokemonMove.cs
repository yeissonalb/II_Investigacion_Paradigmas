using System.Text.Json.Serialization;

namespace ServiceA.Domain;

public record PokemonMove(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("power")] int Power,
    [property: JsonPropertyName("accuracy")] int? Accuracy,
    [property: JsonPropertyName("priority")] int Priority,
    [property: JsonPropertyName("damageClass")] string DamageClass
);
