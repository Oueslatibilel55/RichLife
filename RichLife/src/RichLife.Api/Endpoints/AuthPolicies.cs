namespace RichLife.Api.Endpoints;

public static class AuthPolicies
{
    /// <summary>Staff: the admin panel (/api/admin/*).</summary>
    public const string Admin = "admin";

    /// <summary>
    /// Signed in and NOT an admin — the game itself (/api/game/*). Admins are staff, not
    /// players: they have no company, no cash and no place on the leaderboard.
    /// </summary>
    public const string Player = "player";
}
