using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class BusinessCatalogueEntryConfiguration : IEntityTypeConfiguration<BusinessCatalogueEntry>
{
    public void Configure(EntityTypeBuilder<BusinessCatalogueEntry> b)
    {
        b.ToTable("catalogue_businesses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(BusinessCatalogueEntry.MaxIdLength);
        b.Property(x => x.Name).HasMaxLength(BusinessCatalogueEntry.MaxNameLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(BusinessCatalogueEntry.MaxDescriptionLength).IsRequired();
        b.Property(x => x.OpeningCost).HasPrecision(20, 4);
        b.Property(x => x.BaseIncomePerSecond).HasPrecision(20, 6);
        b.Property(x => x.MonthlySalaryCost).HasPrecision(20, 4);

        // Assets have no life outside their business, so they are owned: loaded with it
        // every time, deleted with it, and keyed only within it ("menu-item" may exist
        // under two different businesses).
        b.OwnsMany(x => x.AvailableAssets, a =>
        {
            a.ToTable("catalogue_assets");
            a.WithOwner().HasForeignKey("BusinessCatalogueId");
            a.HasKey("BusinessCatalogueId", nameof(AssetCatalogueEntry.Id));
            a.Property(x => x.Id).HasMaxLength(BusinessCatalogueEntry.MaxIdLength);
            a.Property(x => x.Name).HasMaxLength(BusinessCatalogueEntry.MaxNameLength).IsRequired();
            a.Property(x => x.Price).HasPrecision(20, 4);
            a.Property(x => x.FixedIncomePerSecond).HasPrecision(20, 6);
        });

        // The property returns a sorted copy; EF must read and write the backing list.
        b.Navigation(x => x.AvailableAssets).HasField("_availableAssets")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
