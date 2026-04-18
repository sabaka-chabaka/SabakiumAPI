using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SabakiumAPI.Data;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await db.Users.FindAsync(Me);
        if (user == null) return NotFound();
        return Ok(new
        {
            id          = user.Id,
            username    = user.Username,
            displayName = user.DisplayName,
            avatarUrl   = AvatarUrl(user.AvatarPath),
        });
    }

    [HttpPatch("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName) || req.DisplayName.Length > 50)
            return BadRequest(new { error = "Имя должно быть от 1 до 50 символов" });

        var user = await db.Users.FindAsync(Me);
        if (user == null) return NotFound();
        user.DisplayName = req.DisplayName.Trim();
        await db.SaveChangesAsync();
        return Ok(new { displayName = user.DisplayName });
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Файл не выбран" });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Файл слишком большой (макс. 5 МБ)" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
            return BadRequest(new { error = "Допустимые форматы: jpg, png, webp, gif" });

        var user = await db.Users.FindAsync(Me);
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(user.AvatarPath))
        {
            var oldFull = Path.Combine(env.WebRootPath, user.AvatarPath.TrimStart('/'));
            if (System.IO.File.Exists(oldFull)) System.IO.File.Delete(oldFull);
        }

        var avatarsDir = Path.Combine(env.WebRootPath, "avatars");
        Directory.CreateDirectory(avatarsDir);

        var fileName = $"{Me}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(avatarsDir, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        user.AvatarPath = $"/avatars/{fileName}";
        await db.SaveChangesAsync();

        return Ok(new { avatarUrl = AvatarUrl(user.AvatarPath) });
    }

    private string? AvatarUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}{path}";
    }
}

public record UpdateDisplayNameRequest(string DisplayName);