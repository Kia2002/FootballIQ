using FootballIQ.Domain.Entities;

namespace FootballIQ.Application.Interfaces;

/// <summary>Provides access to Player data, independent of how it is persisted.</summary>
public interface IPlayerRepository
{
    /// <summary>Returns the player with the given ID, or throws PlayerNotFoundException if none exists.</summary>
    Task<Player> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns all players.</summary>
    Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns the player with the given StatsBomb ID, or null if not yet ingested.</summary>
    Task<Player?> GetByStatsBombIdAsync(int statsBombPlayerId, CancellationToken cancellationToken);

    /// <summary>Adds a new player to be persisted on the next SaveChanges call.</summary>
    Task AddAsync(Player player, CancellationToken cancellationToken);

    /// <summary>Persists all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
