using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class DiamondTransactionConfiguration : IEntityTypeConfiguration<DiamondTransaction>
{
    public void Configure(EntityTypeBuilder<DiamondTransaction> b)
    {
        b.ToTable("diamond_transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(30).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(DiamondTransaction.MaxDetailLength);

        // History is read per company, newest first.
        b.HasIndex(x => new { x.CompanyId, x.CreatedAt });
    }
}
