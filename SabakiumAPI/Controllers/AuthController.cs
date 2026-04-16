using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;
using SabakiumAPI.Models;
using SabakiumAPI.Services;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, AuthService auth) : ControllerBase
{
    public record RegisterRequest(string Username, string DisplayName, string Password);
    public record LoginRequest(string Username, string Password);
    public record AuthResponse(string Token, int UserId, string Username, string DisplayName);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || req.Password.Length < 6)
            return BadRequest(new { error = "Неверные данные" });

        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { error = "Имя пользователя уже занято" });

        var user = new User
        {
            Username = req.Username.Trim().ToLower(),
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(auth.GenerateToken(user), user.Id, user.Username, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username.ToLower());

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Неверное имя пользователя или пароль" });

        return Ok(new AuthResponse(auth.GenerateToken(user), user.Id, user.Username, user.DisplayName));
    }
}