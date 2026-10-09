using RichLife.Application.DTOs;
using RichLife.Domain.Banking;
using RichLife.Domain.Entities;

namespace RichLife.Application.Mapping;

public static class BankMapper
{
    public static LoanOfferDto ToDto(LoanOffer o) => new(
        o.Id, o.Bank.Id, o.Bank.Name, o.Bank.Icon,
        o.Amount, o.InterestRate, o.TotalRepay, o.Installments, o.InstallmentAmount);

    public static LoanDto ToDto(Loan l) => new(
        l.Id, l.BankId, l.BankName, l.BankIcon,
        l.Principal, l.InterestRate, l.TotalRepay, l.Installments, l.InstallmentAmount,
        l.Paid, l.Penalties, l.Outstanding, l.MissedPayments,
        l.CreatedAt, l.NextPaymentAt, l.RepaidAt, l.Forgiven);

    public static LoanPaymentDto? ToDto(LoanCollection? c) => c is null
        ? null
        : new(c.Loan.BankName, c.Paid, c.Penalty, c.Loan.Outstanding, !c.Loan.IsActive);
}
