using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;
using SabakiumAPI.Hubs;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private string? AvatarUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}{path}";
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var user = await db.Users
            .Include(u => u.Posts)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        return Ok(new {
            id          = user.Id,
            username    = user.Username,
            displayName = user.DisplayName,
            avatarUrl   = AvatarUrl(user.AvatarPath),
            online      = UserConnections.Get(user.Id) != null,
            postCount   = user.Posts.Count,
        });
    }

    [HttpGet("{id}/posts")]
    public async Task<IActionResult> GetUserPosts(int id)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == id);
        if (!exists) return NotFound();

        var posts = await db.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Where(p => p.UserId == id)
            .OrderByDescending(p => p.Id)
            .Take(50)
            .ToListAsync();

        var req = HttpContext.Request;
        string? FileUrl(string? p) => string.IsNullOrEmpty(p) ? null : $"{req.Scheme}://{req.Host}{p}";

        return Ok(posts.Select(p => new {
            id           = p.Id,
            content      = p.Content,
            createdAt    = p.CreatedAt,
            userId       = p.UserId,
            username     = p.User.Username,
            displayName  = p.User.DisplayName,
            avatarUrl    = FileUrl(p.User.AvatarPath),
            imageUrl     = FileUrl(p.ImagePath),
            likesCount   = p.Likes.Count,
            likedByMe    = false,
            commentsCount = p.Comments.Count,
        }));
    }
}