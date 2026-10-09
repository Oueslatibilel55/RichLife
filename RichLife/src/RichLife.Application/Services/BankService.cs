using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Application.Mapping;
using RichLife.Domain;
using RichLife.Domain.Banking;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

/// <summary>The bank (contract §6d). Offers come from <see cref="LoanOffers"/>; the rules are on Company.</summary>
public class BankService(
    ICompanyRepository companyRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private const int HistorySize = 10;

    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<BankDto>> GetAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BankDto>("Company not found.");

        return Result.Ok(ToDto(company, UtcNow));
    }

    public async Task<Result<BankDto>> TakeLoanAsync(Guid playerId, string offerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BankDto>("Company not found.");

        var now = UtcNow;
        var offer = LoanOffers.Find(offerId, company.PrestigeLevel, now);
        if (offer is null) return Result.Fail<BankDto>("This offer has expired.");

        var result = company.TakeLoan(offer, now);
        if (!result.IsSuccess) return Result.Fail<BankDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);
        return Result.Ok(ToDto(company, now));
    }

    public async Task<Result<BankDto>> RepayAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BankDto>("Company not found.");

        var now = UtcNow;
        // An installment may have fallen due since the last sync: collect it first, so
        // "repay all" settles exactly what is owed now.
        company.CollectLoanPayments(now);

        var result = company.RepayLoan(now);
        if (!result.IsSuccess) return Result.Fail<BankDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);
        return Result.Ok(ToDto(company, now));
    }

    private static BankDto ToDto(Company company, DateTime now) => new(
        company.Cash,
        LoanOffers.NextRotation(now),
        (int)GameConstants.LoanPaymentInterval.TotalHours,
        GameConstants.LoanPenaltyRate,
        LoanOffers.For(company.PrestigeLevel, now).Select(BankMapper.ToDto).ToList(),
        company.ActiveLoan is { } active ? BankMapper.ToDto(active) : null,
        company.Loans
            .Where(l => !l.IsActive)
            .OrderByDescending(l => l.RepaidAt)
            .Take(HistorySize)
            .Select(BankMapper.ToDto)
            .ToList());
}
