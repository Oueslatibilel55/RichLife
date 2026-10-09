using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> b)
    {
        b.ToTable("loans");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankId).HasMaxLength(60).IsRequired();
        b.Property(x => x.BankName).HasMaxLength(80).IsRequired();
        b.Property(x => x.BankIcon).HasMaxLength(16).IsRequired();
        b.Property(x => x.Principal).HasPrecision(24, 4);
        b.Property(x => x.InterestRate).HasPrecision(8, 6);
        b.Property(x => x.TotalRepay).HasPrecision(24, 4);
        b.Property(x => x.InstallmentAmount).HasPrecision(24, 4);
        b.Property(x => x.Paid).HasPrecision(24, 4);
        b.Property(x => x.Penalties).HasPrecision(24, 4);

        b.Ignore(x => x.Outstanding);
        b.Ignore(x => x.IsActive);

        // At most one active loan per company: the aggregate checks it, the index enforces it.
        b.HasIndex(x => x.CompanyId)
         .IsUnique()
         .HasFilter("\"RepaidAt\" IS NULL")
         .HasDatabaseName("IX_loans_CompanyId_active");
    }
}
