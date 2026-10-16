namespace RichLife.Domain.Common;

internal static class Time
{
    /// <summary>Seconds shared by [<paramref name="a1"/>, <paramref name="a2"/>) and [<paramref name="b1"/>, <paramref name="b2"/>).</summary>
    public static decimal Overlap(DateTime a1, DateTime a2, DateTime b1, DateTime b2)
    {
        var start = a1 > b1 ? a1 : b1;
        var end = a2 < b2 ? a2 : b2;
        return end > start ? (decimal)(end - start).TotalSeconds : 0m;
    }
}
