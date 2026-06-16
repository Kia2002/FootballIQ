using FootballIQ.Domain.Exceptions;

namespace FootballIQ.Domain.Tests;

public class PlayerNotFoundExceptionTests
{
    [Fact]
    public void PlayerNotFoundException_WithPlayerId_IncludesPlayerIdInMessage()
    {
        var playerId = Guid.NewGuid();

        var exception = new PlayerNotFoundException(playerId);

        Assert.Contains(playerId.ToString(), exception.Message);
    }

    [Fact]
    public void PlayerNotFoundException_IsException()
    {
        var exception = new PlayerNotFoundException(Guid.NewGuid());

        Assert.IsAssignableFrom<Exception>(exception);
    }
}
