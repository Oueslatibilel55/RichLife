namespace RichLife.Domain.Operations;

/// <summary>
/// Street a new place opens on — flavour only, picked from the place's id so it is stable.
/// Proper names, not translated. Stored on the place, so this list can change freely.
/// </summary>
public static class StreetNames
{
    private static readonly string[] Streets =
    [
        "Oak Street", "Maple Avenue", "Harbour Road", "Station Square", "Market Street",
        "King's Road", "Elm Grove", "Riverside Drive", "Park Lane", "Mill Road",
        "Victoria Street", "Church Lane", "High Street", "Bridge Street", "Garden Row",
        "Cedar Avenue", "Union Street", "Queen's Walk", "Chapel Street", "Ocean Drive",
        "Liberty Avenue", "Sunset Boulevard", "Pine Street", "Castle Hill", "Canal Street",
        "Willow Way", "Broadway", "Main Street", "Hillside Road", "Lake View",
        "Orchard Lane", "Fifth Avenue", "Grand Parade", "North Road", "Kingsway",
        "Bay Street", "Rose Lane", "Old Town Square", "Airport Road", "Industrial Way",
    ];

    public static string For(Guid placeId)
    {
        var hash = (uint)placeId.GetHashCode();
        var number = 1 + (int)(hash % 180);
        return $"{number} {Streets[(hash / 180) % (uint)Streets.Length]}";
    }
}
