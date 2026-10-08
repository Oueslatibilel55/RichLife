using RichLife.Domain.Entities;

namespace RichLife.Application.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task AddAsync(Player player, CancellationToken ct = default);
    Task<Player?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    void Update(Player player);

    /// <summary>Deletes the player; the database cascades to the company and its businesses.</summary>
    void Remove(Player player);
}
