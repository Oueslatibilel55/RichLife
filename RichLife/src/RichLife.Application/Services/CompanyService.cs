using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Application.Mapping;
using RichLife.Domain;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

public class CompanyService(
    ICompanyRepository companyRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<CompanyDto>> GetOrCreateAsync(
        Guid playerId, string companyName, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);

        if (company is null)
        {
            company = Company.Create(playerId, companyName);
            await companyRepo.AddAsync(company, ct);
            await uow.CommitAsync(ct);
        }

        return Result.Ok(CompanyMapper.ToDto(company, UtcNow));
    }

    /// <summary>
    /// Loads the company and credits offline earnings for the window since the last sync,
    /// capped at <see cref="GameConstants.OfflineCap"/> and counting only automated
    /// businesses. The accrual lives on the aggregate, so this cannot drift from the rules.
    /// </summary>
    public async Task<Result<OfflineEarningsDto>> LoadWithOfflineProgressAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<OfflineEarningsDto>("Company not found.");

        var now = UtcNow;
        var elapsed = now - company.LastSyncAt;
        var cashBefore = company.Cash;

        var earned = company.AccrueOffline(now);
        if (earned > 0m)
        {
            companyRepo.Update(company);
            await uow.CommitAsync(ct);
        }

        return Result.Ok(new OfflineEarningsDto(
            Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
            Earned: earned,
            CashBefore: cashBefore,
            CashAfter: company.Cash,
            Capped: elapsed > GameConstants.OfflineCap,
            Company: CompanyMapper.ToDto(company, now)));
    }

    public async Task<Result<CompanyDto>> PrestigeAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<CompanyDto>("Company not found.");

        // Credit anything earned while away before measuring net worth against the threshold.
        company.AccrueOffline(UtcNow);

        var result = company.Prestige(UtcNow);
        if (!result.IsSuccess) return Result.Fail<CompanyDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(company, UtcNow));
    }

    public async Task<CompanyDto?> GetByPlayerIdAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        return company is null ? null : CompanyMapper.ToDto(company, UtcNow);
    }

    /// <summary>
    /// Accepts the cash figure the client has been simulating locally, clamped by the
    /// aggregate to what could plausibly have been earned since the last sync.
    /// </summary>
    public async Task<Result<SyncResultDto>> SyncAsync(
        Guid playerId, decimal clientCash, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<SyncResultDto>("Company not found.");

        var now = UtcNow;
        var result = company.Sync(clientCash, now);
        if (!result.IsSuccess) return Result.Fail<SyncResultDto>(result.Error!);

        // Every sync (5 s) is also the achievement check, so an unlock is announced within
        // seconds of whatever earned it — a purchase, a level, the earnings ticking past a goal.
        var fresh = company.UnlockAchievements(now);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(new SyncResultDto(
            company.Cash,
            Adjusted: company.Cash != clientCash,
            NewAchievements: fresh.Select(a => new AchievementUnlockedDto(a.Code, a.Title, a.Icon)).ToList()));
    }
}
