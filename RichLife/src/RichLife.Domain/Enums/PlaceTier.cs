namespace RichLife.Domain.Enums;

/// <summary>
/// How busy a place is (features/014-business-places-and-equipment.md). Sets the place's
/// income multiplier (<see cref="GameConstants.PlaceTierMultiplier"/>). Stored as an int —
/// append only, never renumber.
/// </summary>
public enum PlaceTier
{
    Quiet = 1,
    Busy = 2,
    Prime = 3
}
