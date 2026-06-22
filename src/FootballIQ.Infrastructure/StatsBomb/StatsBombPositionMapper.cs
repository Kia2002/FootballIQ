using FootballIQ.Domain.Enums;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Maps StatsBomb's granular position labels (e.g. "Right Center Back") to our 7-value domain Position enum.</summary>
public static class StatsBombPositionMapper
{
    private static readonly Dictionary<string, Position> LabelToPosition = new()
    {
        ["Goalkeeper"] = Position.Goalkeeper,
        ["Right Center Back"] = Position.CentreBack,
        ["Left Center Back"] = Position.CentreBack,
        ["Center Back"] = Position.CentreBack,
        ["Right Back"] = Position.RightBack,
        ["Right Wing Back"] = Position.RightBack,
        ["Left Back"] = Position.LeftBack,
        ["Left Wing Back"] = Position.LeftBack,
        ["Center Defensive Midfield"] = Position.CentreMidfield,
        ["Right Defensive Midfield"] = Position.CentreMidfield,
        ["Left Defensive Midfield"] = Position.CentreMidfield,
        ["Center Midfield"] = Position.CentreMidfield,
        ["Right Center Midfield"] = Position.CentreMidfield,
        ["Left Center Midfield"] = Position.CentreMidfield,
        ["Center Attacking Midfield"] = Position.CentreMidfield,
        ["Right Attacking Midfield"] = Position.Winger,
        ["Left Attacking Midfield"] = Position.Winger,
        ["Right Midfield"] = Position.Winger,
        ["Left Midfield"] = Position.Winger,
        ["Right Wing"] = Position.Winger,
        ["Left Wing"] = Position.Winger,
        ["Center Forward"] = Position.Striker,
        ["Right Center Forward"] = Position.Striker,
        ["Left Center Forward"] = Position.Striker,
        ["Secondary Striker"] = Position.Striker,
    };

    public static Position Map(string statsBombLabel)
    {
        if (LabelToPosition.TryGetValue(statsBombLabel, out var position))
        {
            return position;
        }

        throw new InvalidOperationException($"Unknown StatsBomb position label: '{statsBombLabel}'");
    }
}
