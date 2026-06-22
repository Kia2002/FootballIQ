using System.Globalization;
using System.Text;
using FootballIQ.Infrastructure.Enrichment.Models;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Decides whether a set of Wikidata candidates confidently identifies one player, using the club we already know them by as the disambiguator. A single candidate is trusted outright - Wikidata's club history is often stale (missing a recent transfer/loan), and StatsBomb's full legal names make a wrong-person collision rare. With multiple candidates, the known club must match exactly one; otherwise it's a guess and returns null.</summary>
public static class PlayerDemographicsMatcher
{
    public static DateTime? FindConfidentBirthDate(string targetClubName, IReadOnlyList<WikidataPersonResult> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0].BirthDate;
        }

        var normalizedTarget = Normalize(targetClubName);

        var matches = candidates
            .Where(candidate => candidate.Clubs.Any(club => ClubsMatch(normalizedTarget, club)))
            .ToList();

        return matches.Count == 1 ? matches[0].BirthDate : null;
    }

    private static bool ClubsMatch(string normalizedTarget, string candidateClub)
    {
        var normalizedCandidate = Normalize(candidateClub);
        var targetTokens = normalizedTarget.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return targetTokens.Length > 0 && targetTokens.All(candidateTokens.Contains);
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
