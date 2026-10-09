using RichLife.Domain.Banking;
using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>
/// A bank loan taken by a company. Owned by <see cref="Company"/>, which is the only thing
/// that changes it: every method here is internal and called from the aggregate. Copies the
/// bank's name and icon (the id-plus-copy shape used by businesses and luxury). Taken on
/// <see cref="BaseEntity.CreatedAt"/>.
/// </summary>
public class Loan : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string BankId { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public string BankIcon { get; private set; } = string.Empty;

    public decimal Principal { get; private set; }
    public decimal InterestRate { get; private set; }
    public decimal TotalRepay { get; private set; }
    public int Installments { get; private set; }
    public decimal InstallmentAmount { get; private set; }

    public decimal Paid { get; private set; }
    public decimal Penalties { get; private set; }
    public int MissedPayments { get; private set; }

    /// <summary>When the bank collects next; null once repaid.</summary>
    public DateTime? NextPaymentAt { get; private set; }
    public DateTime? RepaidAt { get; private set; }

    public decimal Outstanding => TotalRepay + Penalties - Paid;
    public bool IsActive => RepaidAt is null;

    private Loan() { }

    internal static Loan Create(Guid companyId, LoanOffer offer, DateTime takenAtUtc) => new()
    {
        CompanyId = companyId,
        BankId = offer.Bank.Id,
        BankName = offer.Bank.Name,
        BankIcon = offer.Bank.Icon,
        Principal = offer.Amount,
        InterestRate = offer.InterestRate,
        TotalRepay = offer.TotalRepay,
        Installments = offer.Installments,
        InstallmentAmount = offer.InstallmentAmount,
        NextPaymentAt = takenAtUtc + GameConstants.LoanPaymentInterval,
        CreatedAt = takenAtUtc,
        UpdatedAt = takenAtUtc,
    };

    /// <summary>The installment due now: a full one, or whatever is left if that is less.</summary>
    internal decimal DueAmount => Math.Min(InstallmentAmount, Outstanding);

    /// <summary>
    /// Records one collection: <paramref name="paid"/> of the <paramref name="due"/> amount.
    /// A shortfall stays owed and costs <see cref="GameConstants.LoanPenaltyRate"/> of itself.
    /// Returns the penalty added.
    /// </summary>
    internal decimal Collect(decimal due, decimal paid, DateTime collectedAtUtc)
    {
        Paid += paid;
        var penalty = 0m;
        var shortfall = due - paid;
        if (shortfall > 0m)
        {
            penalty = decimal.Round(shortfall * GameConstants.LoanPenaltyRate, 2);
            Penalties += penalty;
            MissedPayments++;
        }

        if (Outstanding <= 0m) MarkRepaid(collectedAtUtc);
        else NextPaymentAt += GameConstants.LoanPaymentInterval;

        MarkUpdated();
        return penalty;
    }

    internal void RepayAll(DateTime nowUtc)
    {
        Paid += Outstanding;
        MarkRepaid(nowUtc);
        MarkUpdated();
    }

    private void MarkRepaid(DateTime atUtc)
    {
        RepaidAt = atUtc;
        NextPaymentAt = null;
    }
}
