using System.Globalization;
using System.Text;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Decides whether a set of Wikidata candidates confidently identifies one player, using the club we already know them by as the disambiguator. Never guesses: zero or multiple club-matching candidates both return null.</summary>
public static class PlayerDemographicsMatcher
{
    public static DateTime? FindConfidentBirthDate(string targetClubName, IReadOnlyList<WikidataPersonResult> candidates)
    {
        var normalizedTarget = Normalize(targetClubName);

        var matches = candidates
            .Where(candidate => candidate.Clubs.Any(club => ClubsMatch(normalizedTarget, club)))
            .ToList();

        return matches.Count == 1 ? matches[0].BirthDate : null;
    }

    private static bool ClubsMatch(string normalizedTarget, string candidateClub)
    {
        var normalizedCandidate = Normalize(candidateClub);
        return normalizedCandidate.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedCandidate);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
