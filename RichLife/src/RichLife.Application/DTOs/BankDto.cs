namespace RichLife.Application.DTOs;

// Bank and loans — contract §6d.

public record BankDto(
    decimal Cash,
    DateTime OffersRefreshAt,
    int PaymentIntervalHours,
    decimal PenaltyRate,
    IReadOnlyList<LoanOfferDto> Offers,
    LoanDto? ActiveLoan,
    IReadOnlyList<LoanDto> History);

public record LoanOfferDto(
    string Id,
    string BankId,
    string BankName,
    string BankIcon,
    decimal Amount,
    decimal InterestRate,
    decimal TotalRepay,
    int Installments,
    decimal InstallmentAmount);

public record LoanDto(
    Guid Id,
    string BankId,
    string BankName,
    string BankIcon,
    decimal Principal,
    decimal InterestRate,
    decimal TotalRepay,
    int Installments,
    decimal InstallmentAmount,
    decimal Paid,
    decimal Penalties,
    decimal Outstanding,
    int MissedPayments,
    DateTime TakenAt,
    DateTime? NextPaymentAt,
    DateTime? RepaidAt);

/// <summary>What the bank collected during a /sync or /state call.</summary>
public record LoanPaymentDto(string BankName, decimal Paid, decimal Penalty, decimal Outstanding, bool Repaid);
