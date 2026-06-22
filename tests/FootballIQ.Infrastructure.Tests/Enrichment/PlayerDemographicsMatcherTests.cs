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
    public void FindConfidentBirthDate_WithNoCandidateMatchingClub_ReturnsNull()
    {
        var candidates = new List<WikidataPersonResult>
        {
            new() { BirthDate = new DateTime(1990, 1, 1), Clubs = ["Manchester United"] }
        };

        var result = PlayerDemographicsMatcher.FindConfidentBirthDate("Barcelona", candidates);

        Assert.Null(result);
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
