using System.Net.Http.Json;
using System.Text.Json;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Looks up player birth dates and club history on Wikidata: a fuzzy name search per name to find candidate entity IDs, then one batched SPARQL query to fetch facts for all of them at once.</summary>
public class WikidataClient : IWikidataClient
{
    private readonly HttpClient _httpClient;

    public WikidataClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FootballIQ-Scout/1.0 (https://github.com/Kia2002/FootballIQ)");
        }
    }

    public async Task<Dictionary<string, List<WikidataPersonResult>>> SearchByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var entityIdsByName = new Dictionary<string, List<string>>();

        foreach (var name in names)
        {
            entityIdsByName[name] = await SearchEntityIdsAsync(name, ct);
        }

        var allEntityIds = entityIdsByName.Values.SelectMany(ids => ids).Distinct().ToList();
        if (allEntityIds.Count == 0)
        {
            return new Dictionary<string, List<WikidataPersonResult>>();
        }

        var factsByEntityId = await FetchFactsAsync(allEntityIds, ct);

        return entityIdsByName.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Where(factsByEntityId.ContainsKey).Select(id => factsByEntityId[id]).ToList());
    }

    private async Task<List<string>> SearchEntityIdsAsync(string name, CancellationToken ct)
    {
        var url = $"https://www.wikidata.org/w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(name)}&language=en&type=item&format=json&limit=5";
        var response = await _httpClient.GetFromJsonAsync<WikidataSearchResponse>(url, ct);

        return response?.Search.Select(r => r.Id).ToList() ?? [];
    }

    private async Task<Dictionary<string, WikidataPersonResult>> FetchFactsAsync(List<string> entityIds, CancellationToken ct)
    {
        var valuesClause = string.Join(" ", entityIds.Select(id => $"wd:{id}"));
        var query = $@"
            SELECT ?entity ?birthDate ?clubLabel WHERE {{
              VALUES ?entity {{ {valuesClause} }}
              ?entity wdt:P569 ?birthDate.
              ?entity p:P54 ?clubStatement.
              ?clubStatement ps:P54 ?club.
              ?club rdfs:label ?clubLabel.
              FILTER(LANG(?clubLabel) = 'en')
            }}";

        var url = $"https://query.wikidata.org/sparql?query={Uri.EscapeDataString(query)}&format=json";
        var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, ct);

        var results = new Dictionary<string, WikidataPersonResult>();

        foreach (var binding in response.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            var entityUri = binding.GetProperty("entity").GetProperty("value").GetString()!;
            var entityId = entityUri.Split('/').Last();
            var birthDate = DateTime.Parse(binding.GetProperty("birthDate").GetProperty("value").GetString()!);
            var clubLabel = binding.GetProperty("clubLabel").GetProperty("value").GetString()!;

            if (!results.TryGetValue(entityId, out var existing))
            {
                existing = new WikidataPersonResult { BirthDate = birthDate, Clubs = [] };
                results[entityId] = existing;
            }

            existing.Clubs.Add(clubLabel);
        }

        return results;
    }

    private record WikidataSearchResponse
    {
        public List<WikidataSearchResult> Search { get; init; } = [];
    }

    private record WikidataSearchResult
    {
        public string Id { get; init; } = string.Empty;
    }
}
