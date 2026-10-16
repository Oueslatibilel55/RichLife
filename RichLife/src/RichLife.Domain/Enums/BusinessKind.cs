namespace RichLife.Domain.Enums;

/// <summary>
/// How a business is played (features/015-catalogue-like-road-to-rich.md): buy vehicles into
/// depots, open sites of different formats and level them, or fill a place with machines. The
/// rules are the same (places and units); the kind sets the words and cards the client shows.
/// Stored as an int — append only, never renumber.
/// </summary>
public enum BusinessKind
{
    Fleet = 1,
    Sites = 2,
    Equipment = 3
}
