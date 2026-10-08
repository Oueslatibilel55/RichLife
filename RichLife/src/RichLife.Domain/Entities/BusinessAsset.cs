using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>
/// An asset owned by a Business (truck, car, hotel room, solar panel...).
/// income/s = PurchasePrice * 0.0005
/// </summary>
public class BusinessAsset : BaseEntity
{
    public Guid BusinessId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PurchasePrice { get; private set; }
    public decimal CurrentValue { get; private set; }

    private BusinessAsset() { }

    public static BusinessAsset Create(Guid businessId, string name, decimal purchasePrice, decimal incomePerSecond)
        => new()
        {
            BusinessId = businessId,
            Name = name,
            PurchasePrice = purchasePrice,
            CurrentValue = purchasePrice,
            IncomePerSecond = incomePerSecond  // ← stocker l'income
        };
    public decimal IncomePerSecond { get; private set; }

}
