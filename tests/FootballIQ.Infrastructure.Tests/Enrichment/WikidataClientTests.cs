using FootballIQ.Infrastructure.Enrichment;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class WikidataClientTests
{
    [Fact(Skip = "Hits the real Wikidata network — run manually to sanity-check coverage, not part of CI.")]
    public async Task SearchByNamesAsync_WithRealMessiName_FindsBarcelonaAndCorrectBirthDate()
    {
        var client = new WikidataClient(new HttpClient());

        var results = await client.SearchByNamesAsync(["Lionel Messi"], CancellationToken.None);

        Assert.True(results.ContainsKey("Lionel Messi"));
        var candidates = results["Lionel Messi"];
        Assert.Contains(candidates, c => c.Clubs.Any(club => club.Contains("Barcelona")) && c.BirthDate.Year == 1987);
    }
}
