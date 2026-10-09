using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>
/// One line of a company's diamond ledger: every gain and every spend, with the balance after
/// it. Append-only and written only by <see cref="Company"/>. The aggregate never loads the
/// ledger — <see cref="Company.DiamondLedger"/> holds just the lines added in this unit of work.
/// </summary>
public class DiamondTransaction : BaseEntity
{
    public const int MaxDetailLength = 200;

    public Guid CompanyId { get; private set; }
    public int Amount { get; private set; }
    public int Balance { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Detail { get; private set; }

    private DiamondTransaction() { }

    internal static DiamondTransaction Create(
        Guid companyId, int amount, int balance, string reason, string? detail, DateTime atUtc) => new()
    {
        CompanyId = companyId,
        Amount = amount,
        Balance = balance,
        Reason = reason,
        Detail = detail,
        CreatedAt = atUtc,
        UpdatedAt = atUtc,
    };
}
