namespace RichLife.Domain.Catalogue;

/// <summary>
/// A first name a hired manager can get ("Lucy"). Game content, like the business
/// catalogue: seeded by migration, stored in <c>manager_names</c>, never created at runtime.
/// </summary>
public sealed class ManagerName
{
    public const int MaxLength = 40;

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private ManagerName() { }

    /// <summary>A new name to insert; the database assigns the id (identity column).</summary>
    public static ManagerName New(string name) => Create(0, name.Trim());

    /// <summary>Validation shared by the admin endpoint; null when the name is acceptable.</summary>
    public static string? Validate(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxLength
            ? $"Name is required and must be at most {MaxLength} characters."
            : null;

    /// <summary>Seeded rows come from the migration; this exists for tests and fixtures.</summary>
    public static ManagerName Create(int id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxLength) throw new ArgumentException($"At most {MaxLength} characters.", nameof(name));
        return new ManagerName { Id = id, Name = name };
    }
}
