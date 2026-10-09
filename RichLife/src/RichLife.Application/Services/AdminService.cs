using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Banking;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Common;

namespace RichLife.Application.Services;

/// <summary>
/// Admin panel: stats, player management, manager names. Reads go through
/// <see cref="IAdminReadRepository"/>; every write goes through an aggregate method.
/// </summary>
public class AdminService(
    IAdminReadRepository reads,
    IPlayerRepository playerRepo,
    ICompanyRepository companyRepo,
    IManagerNameRepository managerNameRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private const int MaxPlayersListed = 200;

    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default)
        => reads.GetStatsAsync(UtcNow, ct);

    public Task<IReadOnlyList<AdminPlayerDto>> GetPlayersAsync(string? search, CancellationToken ct = default)
        => reads.GetPlayersAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), MaxPlayersListed, ct);

    // -- Players --------------------------------------------------------------

    public async Task<Result<AdminPlayerDto>> SetAdminAsync(
        Guid actorId, Guid playerId, bool isAdmin, CancellationToken ct = default)
    {
        if (actorId == playerId && !isAdmin)
            return Result.Fail<AdminPlayerDto>("You cannot remove your own admin role.");

        var player = await playerRepo.GetByIdAsync(playerId, ct);
        if (player is null) return Result.Fail<AdminPlayerDto>("Player not found.");

        player.SetAdmin(isAdmin);
        playerRepo.Update(player);
        await uow.CommitAsync(ct);

        return await ReloadAsync(playerId, ct);
    }

    public async Task<Result<AdminPlayerDto>> SetCashAsync(
        Guid playerId, decimal cash, CancellationToken ct = default)
    {
        if (await playerRepo.GetByIdAsync(playerId, ct) is null)
            return Result.Fail<AdminPlayerDto>("Player not found.");

        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<AdminPlayerDto>("Player has no company.");

        var result = company.AdminSetCash(cash);
        if (!result.IsSuccess) return Result.Fail<AdminPlayerDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return await ReloadAsync(playerId, ct);
    }

    public async Task<Result<AdminPlayerDto>> ResetAsync(Guid playerId, CancellationToken ct = default)
    {
        if (await playerRepo.GetByIdAsync(playerId, ct) is null)
            return Result.Fail<AdminPlayerDto>("Player not found.");

        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<AdminPlayerDto>("Player has no company.");

        company.AdminReset(UtcNow);
        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return await ReloadAsync(playerId, ct);
    }

    public async Task<Result> DeleteAsync(Guid actorId, Guid playerId, CancellationToken ct = default)
    {
        if (actorId == playerId) return Result.Fail("You cannot delete your own account.");

        var player = await playerRepo.GetByIdAsync(playerId, ct);
        if (player is null) return Result.Fail("Player not found.");

        playerRepo.Remove(player);
        await uow.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<AdminPlayerDto>> ForgiveLoanAsync(Guid playerId, CancellationToken ct = default)
    {
        if (await playerRepo.GetByIdAsync(playerId, ct) is null)
            return Result.Fail<AdminPlayerDto>("Player not found.");

        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<AdminPlayerDto>("Player has no company.");

        var result = company.AdminForgiveLoan(UtcNow);
        if (!result.IsSuccess) return Result.Fail<AdminPlayerDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return await ReloadAsync(playerId, ct);
    }

    public async Task<Result<AdminPlayerDto>> AdjustDiamondsAsync(
        Guid playerId, int amount, string? reason, CancellationToken ct = default)
    {
        if (await playerRepo.GetByIdAsync(playerId, ct) is null)
            return Result.Fail<AdminPlayerDto>("Player not found.");

        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<AdminPlayerDto>("Player has no company.");

        var result = company.AdminAdjustDiamonds(amount, reason, UtcNow);
        if (!result.IsSuccess) return Result.Fail<AdminPlayerDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return await ReloadAsync(playerId, ct);
    }

    // -- Bank -------------------------------------------------------------------

    public Task<IReadOnlyList<AdminLoanDto>> GetLoansAsync(bool activeOnly, CancellationToken ct = default)
        => reads.GetLoansAsync(activeOnly, MaxPlayersListed, ct);

    /// <summary>The banks are rules in code; their usage comes from the loans table.</summary>
    public async Task<IReadOnlyList<AdminBankDto>> GetBanksAsync(CancellationToken ct = default)
    {
        var usage = (await reads.GetBankUsageAsync(ct)).ToDictionary(u => u.BankId);
        return BankCatalog.All.Select(b =>
        {
            var u = usage.GetValueOrDefault(b.Id);
            return new AdminBankDto(
                b.Id, b.Name, b.Icon, b.MinRate, b.MaxRate, b.MinInstallments, b.MaxInstallments,
                u?.LoansTaken ?? 0, u?.ActiveLoans ?? 0, u?.TotalLent ?? 0m);
        }).ToList();
    }

    private async Task<Result<AdminPlayerDto>> ReloadAsync(Guid playerId, CancellationToken ct)
        => await reads.GetPlayerAsync(playerId, ct) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<AdminPlayerDto>("Player not found.");

    // -- Manager names ----------------------------------------------------------

    public Task<IReadOnlyList<ManagerNameDto>> GetManagerNamesAsync(CancellationToken ct = default)
        => reads.GetManagerNamesAsync(ct);

    public async Task<Result<ManagerNameDto>> AddManagerNameAsync(string? name, CancellationToken ct = default)
    {
        if (ManagerName.Validate(name) is { } error) return Result.Fail<ManagerNameDto>(error);

        var trimmed = name!.Trim();
        if (await managerNameRepo.NameExistsAsync(trimmed, ct))
            return Result.Fail<ManagerNameDto>("Name already exists.");

        var entity = ManagerName.New(trimmed);
        await managerNameRepo.AddAsync(entity, ct);
        await uow.CommitAsync(ct);

        return Result.Ok(new ManagerNameDto(entity.Id, entity.Name, 0));
    }

    public async Task<Result> DeleteManagerNameAsync(int id, CancellationToken ct = default)
    {
        var entity = await managerNameRepo.GetByIdAsync(id, ct);
        if (entity is null) return Result.Fail("Name not found.");

        // The businesses → manager_names foreign key is RESTRICT; say so instead of a 500.
        if (await managerNameRepo.IsInUseAsync(id, ct)) return Result.Fail("Name is used by a business.");

        managerNameRepo.Remove(entity);
        await uow.CommitAsync(ct);
        return Result.Ok();
    }
}
