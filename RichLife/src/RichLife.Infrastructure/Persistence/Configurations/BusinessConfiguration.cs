using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> b)
    {
        b.ToTable("businesses");
        b.HasKey(x => x.Id);
        b.Property(x => x.CatalogueId).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.OpeningCost).HasPrecision(20, 4);
        b.Property(x => x.GrossIncomePerSecond).HasPrecision(20, 6);
        b.Property(x => x.MonthlySalaryCost).HasPrecision(20, 4);
        b.Property(x => x.AskingPrice).HasPrecision(20, 4);

        // A company owns at most one of each catalogue entry. The aggregate enforces
        // this; the index makes a concurrent double-open fail at the database too.
        b.HasIndex(x => new { x.CompanyId, x.CatalogueId }).IsUnique();

        // Cross-aggregate reference by id only — no navigation. Restrict, because an
        // entry somebody owns must never disappear; retiring it is IsActive = false.
        b.HasOne<BusinessCatalogueEntry>()
         .WithMany()
         .HasForeignKey(x => x.CatalogueId)
         .OnDelete(DeleteBehavior.Restrict);

        // The hired manager: id into manager_names plus a copy of the name, the same
        // shape as CatalogueId/Name above. Restrict — a name in use must not vanish.
        b.Property(x => x.ManagerName).HasMaxLength(ManagerName.MaxLength);
        b.HasOne<ManagerName>()
         .WithMany()
         .HasForeignKey(x => x.ManagerNameId)
         .OnDelete(DeleteBehavior.Restrict);

        // Computed properties — do NOT map to DB
        b.Ignore(x => x.SalaryCostPerSecond);
        b.Ignore(x => x.NetIncomePerSecond);
        b.Ignore(x => x.TotalValue);
        b.Ignore(x => x.ManagerCost);
        b.Ignore(x => x.LevelMultiplier);
        b.Ignore(x => x.IsMaxLevel);
        b.Ignore(x => x.NextLevelCost);
        b.Ignore(x => x.NextLevelIncomePerSecond);
        // Level: the AddBusinessLevels migration backfills existing rows with 1.

        b.HasMany(x => x.Assets)
         .WithOne()
         .HasForeignKey(a => a.BusinessId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
