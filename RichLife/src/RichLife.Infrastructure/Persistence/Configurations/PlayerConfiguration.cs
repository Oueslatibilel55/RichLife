using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("players");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.Country).HasMaxLength(2);
        b.HasIndex(x => x.Username).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.RefreshToken);

        b.Ignore(x => x.DomainEvents);

        b.HasOne(x => x.Company)
         .WithOne()
         .HasForeignKey<Company>(c => c.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
