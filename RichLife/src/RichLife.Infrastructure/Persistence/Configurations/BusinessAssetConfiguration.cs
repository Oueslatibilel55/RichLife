using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

/// <summary>A business's units. The table keeps its original name, "BusinessAssets".</summary>
public class BusinessAssetConfiguration : IEntityTypeConfiguration<BusinessAsset>
{
    public void Configure(EntityTypeBuilder<BusinessAsset> b)
    {
        b.ToTable("BusinessAssets");
        b.HasKey(x => x.Id);
        b.Property(x => x.CatalogueId).HasMaxLength(BusinessCatalogueEntry.MaxIdLength);

        // Every unit stands in a place of its own business. Both cascade from the business;
        // a place is never deleted on its own.
        b.HasOne<BusinessPlace>()
         .WithMany()
         .HasForeignKey(x => x.PlaceId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
