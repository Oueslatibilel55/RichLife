using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Player>        Players        => Set<Player>();
    public DbSet<Company>       Companies      => Set<Company>();
    public DbSet<Business>      Businesses     => Set<Business>();
    public DbSet<BusinessAsset> BusinessAssets => Set<BusinessAsset>();
    public DbSet<Asset>         Assets         => Set<Asset>();
    public DbSet<LuxuryAsset>   LuxuryAssets   => Set<LuxuryAsset>();
    public DbSet<BusinessCatalogueEntry> Catalogue => Set<BusinessCatalogueEntry>();
    public DbSet<ManagerName>   ManagerNames   => Set<ManagerName>();
    public DbSet<LuxuryCatalogueEntry> LuxuryCatalogue => Set<LuxuryCatalogueEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        // Every Guid key here is assigned by the domain — the static factories
        // (Business.Create, Company.Create, ...) stamp Id = Guid.NewGuid(). EF's
        // default for a Guid key is ValueGeneratedOnAdd, under which it decides
        // whether an entity discovered through a navigation is new by asking
        // whether its key is still default. Ours never is, so a freshly opened
        // business was tracked as Modified instead of Added and SaveChanges
        // issued an UPDATE that matched no row (DbUpdateConcurrencyException).
        foreach (var property in mb.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.IsPrimaryKey() && p.ClrType == typeof(Guid)))
        {
            property.ValueGenerated = ValueGenerated.Never;
        }

        base.OnModelCreating(mb);
    }
}
