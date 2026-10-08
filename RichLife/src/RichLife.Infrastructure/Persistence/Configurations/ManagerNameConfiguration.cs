using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Catalogue;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class ManagerNameConfiguration : IEntityTypeConfiguration<ManagerName>
{
    public void Configure(EntityTypeBuilder<ManagerName> b)
    {
        b.ToTable("manager_names");
        b.HasKey(x => x.Id);
        // Identity column: the rows are content, seeded by migration (no HasData — the
        // database owns them, exactly like the business catalogue).
        b.Property(x => x.Id).UseIdentityByDefaultColumn();
        b.Property(x => x.Name).HasMaxLength(ManagerName.MaxLength).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}
