using RichLife.Application.DTOs;

namespace RichLife.Application.Interfaces;

public interface ILeaderboardRepository
{
    /// <summary>Top companies by all-time earnings, highest first.</summary>
    Task<IReadOnlyList<LeaderboardEntryDto>> GetTopByEarningsAsync(int take, CancellationToken ct = default);

    /// <summary>
    /// 1-based leaderboard position for a company with these earnings, and how many players
    /// are ranked (players with a company, admins excluded).
    /// </summary>
    Task<(int Rank, int Total)> GetRankAsync(decimal allTimeEarnings, CancellationToken ct = default);
}
