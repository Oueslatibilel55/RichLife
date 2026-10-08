using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>Luxury asset owned directly by the Company (jet, yacht, island).</summary>
public class Asset : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PurchasePrice { get; private set; }
    public decimal CurrentValue { get; private set; }
    public decimal PassiveIncomeBonus { get; private set; }

    private Asset() { }

    public static Asset Create(Guid companyId, string name, decimal price, decimal incomeBonus)
        => new() { CompanyId = companyId, Name = name, PurchasePrice = price, CurrentValue = price, PassiveIncomeBonus = incomeBonus };
}
