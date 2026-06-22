namespace FootballIQ.Infrastructure.Enrichment.Models;

/// <summary>One Wikidata candidate match for a player name search: a birth date and the clubs they're recorded as having played for.</summary>
public record WikidataPersonResult
{
    public DateTime BirthDate { get; init; }
    public List<string> Clubs { get; init; } = [];
}
