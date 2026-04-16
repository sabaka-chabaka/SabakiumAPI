using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;
using SabakiumAPI.Hubs;
using SabakiumAPI.Models;
using System.Security.Claims;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController(AppDbContext db, IHubContext<FeedHub> hub) : ControllerBase
{
    public record PostDto(int Id, string Content, DateTime CreatedAt, int UserId, string Username, string DisplayName);
    public record CreatePostRequest(string Content);

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] int? before, [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);

        var query = db.Posts
            .Include(p => p.User)
            .OrderByDescending(p => p.Id)
            .AsQueryable();

        if (before.HasValue)
            query = query.Where(p => p.Id < before.Value);

        var posts = await query
            .Take(limit)
            .Select(p => new PostDto(p.Id, p.Content, p.CreatedAt, p.UserId, p.User.Username, p.User.DisplayName))
            .ToListAsync();

        return Ok(posts);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreatePostRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content) || req.Content.Length > 2000)
            return BadRequest(new { error = "Пост не может быть пустым или длиннее 2000 символов" });

        var post = new Post
        {
            Content = req.Content.Trim(),
            UserId = CurrentUserId
        };

        db.Posts.Add(post);
        await db.SaveChangesAsync();

        await db.Entry(post).Reference(p => p.User).LoadAsync();

        var dto = new PostDto(post.Id, post.Content, post.CreatedAt,
            post.UserId, post.User.Username, post.User.DisplayName);

        await hub.Clients.All.SendAsync("NewPost", dto);

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await db.Posts.FindAsync(id);
        if (post is null) return NotFound();
        if (post.UserId != CurrentUserId) return Forbid();

        db.Posts.Remove(post);
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("DeletePost", id);
        return NoContent();
    }
}