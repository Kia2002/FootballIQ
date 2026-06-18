using System.Text.Json.Serialization;

namespace FootballIQ.Infrastructure.StatsBomb.Models;

/// <summary>
/// One entry from a StatsBomb events/{match_id}.json file. Every event shares this envelope;
/// exactly one of the type-specific properties (Pass, Shot, ...) is populated depending on Type.Name.
/// </summary>
public record StatsBombEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("period")]
    public int Period { get; init; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("minute")]
    public int Minute { get; init; }

    [JsonPropertyName("second")]
    public int Second { get; init; }

    [JsonPropertyName("type")]
    public StatsBombNamedId Type { get; init; } = null!;

    [JsonPropertyName("team")]
    public StatsBombNamedId Team { get; init; } = null!;

    [JsonPropertyName("player")]
    public StatsBombNamedId? Player { get; init; }

    [JsonPropertyName("position")]
    public StatsBombNamedId? Position { get; init; }

    [JsonPropertyName("location")]
    public List<double>? Location { get; init; }

    [JsonPropertyName("pass")]
    public StatsBombPassData? Pass { get; init; }

    [JsonPropertyName("shot")]
    public StatsBombShotData? Shot { get; init; }
}

/// <summary>The repeated {"id": int, "name": string} shape StatsBomb uses for type, team, player, position, etc.</summary>
public record StatsBombNamedId
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record StatsBombPassData
{
    [JsonPropertyName("recipient")]
    public StatsBombNamedId? Recipient { get; init; }

    [JsonPropertyName("length")]
    public double Length { get; init; }

    [JsonPropertyName("height")]
    public StatsBombNamedId? Height { get; init; }

    [JsonPropertyName("body_part")]
    public StatsBombNamedId? BodyPart { get; init; }

    /// <summary>Present only for an incomplete pass (e.g. "Incomplete", "Out", "Pass Offside"). Absent means completed.</summary>
    [JsonPropertyName("outcome")]
    public StatsBombNamedId? Outcome { get; init; }

    /// <summary>True if this pass directly created a shot. Pairs with <see cref="AssistedShotId"/> to credit xA.</summary>
    [JsonPropertyName("shot_assist")]
    public bool ShotAssist { get; init; }

    /// <summary>The <see cref="StatsBombEvent.Id"/> of the shot this pass assisted, when <see cref="ShotAssist"/> is true.</summary>
    [JsonPropertyName("assisted_shot_id")]
    public string? AssistedShotId { get; init; }
}

public record StatsBombShotData
{
    [JsonPropertyName("statsbomb_xg")]
    public double StatsbombXg { get; init; }

    [JsonPropertyName("outcome")]
    public StatsBombNamedId? Outcome { get; init; }

    [JsonPropertyName("technique")]
    public StatsBombNamedId? Technique { get; init; }

    [JsonPropertyName("body_part")]
    public StatsBombNamedId? BodyPart { get; init; }
}
