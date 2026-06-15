namespace FootballIQ.Domain.Exceptions;

/// <summary>Thrown when a player with the given ID does not exist.</summary>
public class PlayerNotFoundException : Exception
{
    public PlayerNotFoundException(Guid playerId)
        : base($"Player with ID '{playerId}' was not found.")
    {
    }
}
