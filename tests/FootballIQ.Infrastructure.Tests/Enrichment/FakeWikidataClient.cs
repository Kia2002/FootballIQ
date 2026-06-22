using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

/// <summary>Returns fixed, hand-built Wikidata results so enrichment tests don't depend on the real network.</summary>
public class FakeWikidataClient : IWikidataClient
{
    private readonly Dictionary<string, List<WikidataPersonResult>> _resultsByName;

    public FakeWikidataClient(Dictionary<string, List<WikidataPersonResult>> resultsByName)
    {
        _resultsByName = resultsByName;
    }

    public Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var result = names
            .Where(_resultsByName.ContainsKey)
            .ToDictionary(name => name, name => _resultsByName[name]);

        return Task.FromResult(result);
    }
}
