using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

public class Player : AggregateRoot
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiry { get; private set; }

    /// <summary>
    /// Grants the admin role claim, and with it the admin panel. The first admin is set in
    /// the database; after that only an admin can change it (<see cref="SetAdmin"/>), so no
    /// player can promote themselves.
    /// </summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Grants or revokes the admin role. Takes effect at the next token issue.</summary>
    public void SetAdmin(bool isAdmin)
    {
        IsAdmin = isAdmin;
        MarkUpdated();
    }

    // Navigation
    public Company? Company { get; private set; }

    private Player() { }

    public static Player Create(string username, string email, string passwordHash, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new Player
        {
            Username     = username,
            Email        = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Country      = country
        };
    }

    public void SetRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiry = expiry;
        MarkUpdated();
    }

    /// <summary>Invalidates the current refresh token (logout, or rotation failure).</summary>
    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiry = null;
        MarkUpdated();
    }
}
