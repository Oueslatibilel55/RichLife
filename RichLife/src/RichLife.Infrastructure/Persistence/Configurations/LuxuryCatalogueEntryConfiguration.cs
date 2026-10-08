using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class LuxuryCatalogueEntryConfiguration : IEntityTypeConfiguration<LuxuryCatalogueEntry>
{
    public void Configure(EntityTypeBuilder<LuxuryCatalogueEntry> b)
    {
        // Content, seeded by migration (InsertData, not HasData) like the business catalogue.
        b.ToTable("luxury_catalogue");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(60);
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Price).HasPrecision(20, 4);
        b.Property(x => x.ImageUrl).HasMaxLength(300).IsRequired();
        b.Property(x => x.ImageCredit).HasMaxLength(200).IsRequired();
        b.Property(x => x.ImageSourceUrl).HasMaxLength(500).IsRequired();
    }
}
