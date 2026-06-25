using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.Enrichment;

/// <summary>Fills in Player.DateOfBirth using Wikidata, batching name lookups and matching by player name + their known club to avoid guessing on ambiguous names.</summary>
public class WikidataEnrichmentService : IPlayerEnrichmentService
{
    private const int BatchSize = 50;

    private readonly IWikidataClient _client;
    private readonly FootballIQDbContext _context;

    public WikidataEnrichmentService(IWikidataClient client, FootballIQDbContext context)
    {
        _client = client;
        _context = context;
    }

    public async Task<int> EnrichPlayerDemographicsAsync(CancellationToken ct)
    {
        var playersNeedingEnrichment = await _context.Players
            .Where(p => p.DateOfBirth == null)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var clubNameByPlayerId = await GetClubNamesByPlayerIdAsync(playersNeedingEnrichment.Select(p => p.Id), ct);

        var updatedCount = 0;

        foreach (var batch in playersNeedingEnrichment.Chunk(BatchSize))
        {
            var names = batch.Select(p => p.Name).Distinct().ToList();
            var candidatesByName = await _client.SearchByNamesAsync(names, ct);

            foreach (var playerInfo in batch)
            {
                if (!clubNameByPlayerId.TryGetValue(playerInfo.Id, out var clubName))
                {
                    continue;
                }

                if (!candidatesByName.TryGetValue(playerInfo.Name, out var candidates))
                {
                    continue;
                }

                var birthDate = PlayerDemographicsMatcher.FindConfidentBirthDate(clubName, candidates);
                if (birthDate is null)
                {
                    continue;
                }

                var player = await _context.Players.SingleAsync(p => p.Id == playerInfo.Id, ct);
                player.DateOfBirth = DateTime.SpecifyKind(birthDate.Value, DateTimeKind.Utc);
                updatedCount++;
            }

            await _context.SaveChangesAsync(ct);
        }

        return updatedCount;
    }

    private async Task<Dictionary<Guid, string>> GetClubNamesByPlayerIdAsync(IEnumerable<Guid> playerIds, CancellationToken ct)
    {
        var idList = playerIds.ToList();

        var rows = await _context.PlayerSeasonStats
            .Where(s => idList.Contains(s.PlayerId))
            .OrderByDescending(s => s.SeasonId)
            .Select(s => new { s.PlayerId, s.Club.Name })
            .ToListAsync(ct);

        // Ordered descending by season above, so the first row per player is their most recent club -
        // GroupBy preserves source order here because this runs against an in-memory List (LINQ to Objects).
        return rows
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }
}
