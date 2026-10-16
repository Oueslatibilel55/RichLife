namespace RichLife.Domain.Enums;

/// <summary>State of a business unit at a given instant — computed, never stored.</summary>
public enum UnitStatus
{
    Working = 1,
    Broken = 2,
    Repairing = 3
}
