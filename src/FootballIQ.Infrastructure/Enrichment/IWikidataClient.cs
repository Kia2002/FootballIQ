using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Looks up Wikidata candidates for a batch of player names. Infrastructure-internal, like IStatsBombReader — only WikidataEnrichmentService depends on this.</summary>
public interface IWikidataClient
{
    Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct);
}
