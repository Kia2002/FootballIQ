using FootballIQ.Application.Interfaces;
using FootballIQ.Domain.Entities;
using FootballIQ.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.Persistence;

/// <summary>EF Core-backed implementation of IPlayerRepository.</summary>
public class PlayerRepository : IPlayerRepository
{
    private readonly FootballIQDbContext _context;

    public PlayerRepository(FootballIQDbContext context)
    {
        _context = context;
    }

    public async Task<Player> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return player ?? throw new PlayerNotFoundException(id);
    }

    public async Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Players.ToListAsync(cancellationToken);
    }

    public async Task<Player?> GetByStatsBombIdAsync(int statsBombPlayerId, CancellationToken cancellationToken)
    {
        return await _context.Players
            .FirstOrDefaultAsync(p => p.StatsBombPlayerId == statsBombPlayerId, cancellationToken);
    }

    public async Task AddAsync(Player player, CancellationToken cancellationToken)
    {
        await _context.Players.AddAsync(player, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
