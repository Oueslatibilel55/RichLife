using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RichLife.Application.Services;

public class AuthService(IPlayerRepository playerRepo, IUnitOfWork uow, IConfiguration config)
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (await playerRepo.UsernameExistsAsync(req.Username, ct))
            return Result.Fail<AuthResponse>("Username already taken.");

        // Email is unique in the database; check it here so a duplicate is a 400,
        // not an unhandled DbUpdateException.
        if (await playerRepo.GetByEmailAsync(req.Email.ToLowerInvariant(), ct) is not null)
            return Result.Fail<AuthResponse>("Email already registered.");

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var player = Player.Create(req.Username, req.Email, hash, req.Country);

        var refreshToken = GenerateRefreshToken();
        player.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));

        await playerRepo.AddAsync(player, ct);
        await uow.CommitAsync(ct);

        return Result.Ok(GenerateTokens(player));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var player = await playerRepo.GetByEmailAsync(req.Email.ToLowerInvariant(), ct);
        if (player is null || !BCrypt.Net.BCrypt.Verify(req.Password, player.PasswordHash))
            return Result.Fail<AuthResponse>("Invalid credentials.");

        var refreshToken = GenerateRefreshToken();
        player.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));

        playerRepo.Update(player);
        await uow.CommitAsync(ct);

        return Result.Ok(GenerateTokens(player));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var player = await playerRepo.GetByRefreshTokenAsync(refreshToken, ct);

        if (player is null || player.RefreshTokenExpiry < DateTime.UtcNow)
            return Result.Fail<AuthResponse>("Invalid or expired refresh token.");

        // Générer un nouveau refresh token
        var newRefreshToken = GenerateRefreshToken();
        player.SetRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(7));

        playerRepo.Update(player);
        await uow.CommitAsync(ct);

        return Result.Ok(GenerateTokens(player));
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    }

    private AuthResponse GenerateTokens(Player player)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new(ClaimTypes.Name,           player.Username),
            new("country",                 player.Country),
        ];

        // Re-read from the player on every login and refresh, so granting or revoking
        // admin takes effect within one access-token lifetime.
        if (player.IsAdmin) claims.Add(new(ClaimTypes.Role, Roles.Admin));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            player.RefreshToken!,
            player.Id,
            player.Username);
    }
}
