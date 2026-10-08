namespace RichLife.Application.DTOs;

public record RegisterRequest(string Username, string Email, string Password, string Country);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId, string Username);
