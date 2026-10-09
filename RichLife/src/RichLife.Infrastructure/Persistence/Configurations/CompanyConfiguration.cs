using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.Cash).HasPrecision(20, 4);
        b.Property(x => x.PassiveIncomePerSecond).HasPrecision(20, 6);
        b.Property(x => x.AllTimeEarnings).HasPrecision(20, 4);

        b.HasIndex(x => x.AllTimeEarnings);   // leaderboard ordering

        // Computed properties — do NOT map to DB
        b.Ignore(x => x.PrestigeMultiplier);
        b.Ignore(x => x.NetWorth);
        b.Ignore(x => x.IncomePerSecond);
        b.Ignore(x => x.DomainEvents);
        b.Ignore(x => x.ActiveLoan);

        b.HasMany(x => x.Businesses)
         .WithOne()
         .HasForeignKey(biz => biz.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Assets)
         .WithOne()
         .HasForeignKey(a => a.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.LuxuryAssets)
         .WithOne()
         .HasForeignKey(a => a.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Loans)
         .WithOne()
         .HasForeignKey(l => l.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        // Unlocked achievements: owned by the company, keyed (CompanyId, Code). Owned
        // collections load with the company, so no Include is needed in the repository.
        b.OwnsMany(x => x.Achievements, a =>
        {
            a.ToTable("company_achievements");
            a.WithOwner().HasForeignKey("CompanyId");
            a.HasKey("CompanyId", nameof(CompanyAchievement.Code));
            a.Property(x => x.Code).HasMaxLength(60);
        });
        b.Navigation(x => x.Achievements).HasField("_achievements")
         .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
