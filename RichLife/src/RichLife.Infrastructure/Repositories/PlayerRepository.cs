using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Entities;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class PlayerRepository(GameDbContext db) : IPlayerRepository
{
    public Task<Player?> GetByEmailAsync(string email, CancellationToken ct)
        => db.Players.FirstOrDefaultAsync(p => p.Email == email, ct);

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct)
        => db.Players.AnyAsync(p => p.Username == username, ct);

    public async Task AddAsync(Player player, CancellationToken ct)
        => await db.Players.AddAsync(player, ct);

    public Task<Player?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct)
    => db.Players.FirstOrDefaultAsync(p => p.RefreshToken == refreshToken, ct);

    public void Update(Player player)
        => db.Entry(player).State = EntityState.Modified;

    public void Remove(Player player) => db.Players.Remove(player);
}
