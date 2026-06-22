using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class PlayerDemographicsMatcherTests
{
    [Fact]
    public void FindConfidentBirthDate_WithOneCandidateMatchingClub_ReturnsBirthDate()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1987, 6, 24), Clubs = ["FC Barcelona"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Equal(new DateTime(1987, 6, 24), result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithNoCandidates_ReturnsNull()
    {
        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", []);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithOneCandidateNotMatchingClub_ReturnsBirthDateAnyway()
    {
        // A single Wikidata search hit for a player's full legal name is trusted even if its
        // club history doesn't mention the known club: Wikidata's club data is often stale
        // (missing a recent transfer/loan), and StatsBomb's full legal names make a wrong-person
        // collision rare. See LEARNING.md Task 2.8 for the real-world data that drove this.
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["Manchester United"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Equal(new DateTime(1990, 1, 1), result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithTwoCandidatesBothMatchingClub_ReturnsNull()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["FC Barcelona"] },
            new() { BirthDate = new DateTime(1995, 5, 5), Clubs = ["FC Barcelona"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithMultipleCandidatesNoneMatchingClub_ReturnsNull()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["Manchester United"] },
            new() { BirthDate = new DateTime(1995, 5, 5), Clubs = ["Liverpool"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Null(result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithClubNameContainingExtraWords_StillMatches()
    {
        // Wikidata's English label is often "RC Celta de Vigo" while our DB stores "Celta Vigo" -
        // a plain substring check fails here because of the inserted "de". Token-based matching
        // should still disambiguate correctly between two candidates.
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["RC Celta de Vigo"] },
            new() { BirthDate = new DateTime(1995, 5, 5), Clubs = ["Manchester United"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Celta Vigo", candidates);

        Assert.Equal(new DateTime(1990, 1, 1), result);
    }

    [Fact]
    public void FindConfidentBirthDate_WithAccentedClubName_StillMatches()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1992, 3, 3), Clubs = ["Deportivo Alaves"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Deportivo Alavés", candidates);

        Assert.Equal(new DateTime(1992, 3, 3), result);
    }
}
