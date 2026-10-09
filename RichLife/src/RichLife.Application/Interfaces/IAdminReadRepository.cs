using RichLife.Application.DTOs;

namespace RichLife.Application.Interfaces;

/// <summary>
/// Read-only projections for the admin panel. Aggregates across every player, so it
/// queries straight into DTOs instead of loading aggregates.
/// </summary>
public interface IAdminReadRepository
{
    Task<AdminStatsDto> GetStatsAsync(DateTime nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPlayerDto>> GetPlayersAsync(string? search, int take, CancellationToken ct = default);
    Task<AdminPlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<ManagerNameDto>> GetManagerNamesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminLoanDto>> GetLoansAsync(bool activeOnly, int take, CancellationToken ct = default);
    Task<IReadOnlyList<BankUsage>> GetBankUsageAsync(CancellationToken ct = default);

    /// <summary>Owners per luxury catalogue id (ids nobody owns are absent).</summary>
    Task<IReadOnlyDictionary<string, int>> GetLuxuryOwnersAsync(CancellationToken ct = default);
}
