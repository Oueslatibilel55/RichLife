using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class LuxuryAssetConfiguration : IEntityTypeConfiguration<LuxuryAsset>
{
    public void Configure(EntityTypeBuilder<LuxuryAsset> b)
    {
        b.ToTable("LuxuryAssets");
        b.HasKey(x => x.Id);
        b.Property(x => x.CatalogueId).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Cost).HasPrecision(20, 4);
        b.Property(x => x.IncomeMultiplierBonus).HasPrecision(20, 6);
        b.Property(x => x.ImageUrl).HasMaxLength(300).IsRequired();
        b.Property(x => x.ImageCredit).HasMaxLength(200).IsRequired();

        // One of each item per company: the aggregate checks it, the index enforces it.
        b.HasIndex(x => new { x.CompanyId, x.CatalogueId }).IsUnique();

        // Id + copy, like Business → catalogue. Restrict: an owned item must not vanish.
        b.HasOne<LuxuryCatalogueEntry>()
         .WithMany()
         .HasForeignKey(x => x.CatalogueId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
