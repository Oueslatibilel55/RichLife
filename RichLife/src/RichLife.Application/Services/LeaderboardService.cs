using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;

namespace RichLife.Application.Services;

public class LeaderboardService(ILeaderboardRepository leaderboardRepo)
{
    private const int MaxTake = 100;

    /// <summary>
    /// Ranks players by all-time earnings. Net worth would be the more natural metric,
    /// but it is computed from the whole business graph and cannot be ordered in SQL —
    /// ranking on it would need a persisted, incrementally maintained column.
    /// </summary>
    public Task<IReadOnlyList<LeaderboardEntryDto>> GetTopAsync(int take, CancellationToken ct = default)
        => leaderboardRepo.GetTopByEarningsAsync(Math.Clamp(take, 1, MaxTake), ct);
}
