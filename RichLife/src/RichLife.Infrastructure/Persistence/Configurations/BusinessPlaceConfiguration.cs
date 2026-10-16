using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class BusinessPlaceConfiguration : IEntityTypeConfiguration<BusinessPlace>
{
    public void Configure(EntityTypeBuilder<BusinessPlace> b)
    {
        b.ToTable("business_places");
        b.HasKey(x => x.Id);
        // A copy of the catalogue kind, not a reference: removing a kind leaves owned places alone.
        b.Property(x => x.KindId).HasMaxLength(BusinessCatalogueEntry.MaxIdLength).IsRequired();
        b.Property(x => x.Name).HasMaxLength(BusinessCatalogueEntry.MaxNameLength).IsRequired();
        b.Property(x => x.Street).HasMaxLength(80).IsRequired();
        b.Property(x => x.Price).HasPrecision(20, 4);
        b.Property(x => x.PurchasePrice).HasPrecision(20, 4);
        b.Property(x => x.BaseIncomePerSecond).HasPrecision(20, 6);

        b.Ignore(x => x.Multiplier);
        b.Ignore(x => x.Capacity);
        b.Ignore(x => x.IsMaxLevel);
        b.Ignore(x => x.NextLevelCost);
    }
}
