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
    public record PostDto(
        int Id, string Content, DateTime CreatedAt,
        int UserId, string Username, string DisplayName,
        string? AvatarUrl, string? ImageUrl,
        int LikesCount, bool LikedByMe,
        int CommentsCount);

    public record CreatePostRequest(string Content);

    private string? GetFileUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}{path}";
    }

    private int? TryGetCurrentUserId()
    {
        var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return val is null ? null : int.Parse(val);
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] int? before, [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        var meId = TryGetCurrentUserId();

        var query = db.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .OrderByDescending(p => p.Id)
            .AsQueryable();

        if (before.HasValue)
            query = query.Where(p => p.Id < before.Value);

        var posts = await query.Take(limit).ToListAsync();

        var dtos = posts.Select(p => new PostDto(
            p.Id, p.Content, p.CreatedAt,
            p.UserId, p.User.Username, p.User.DisplayName,
            GetFileUrl(p.User.AvatarPath), GetFileUrl(p.ImagePath),
            p.Likes.Count,
            meId.HasValue && p.Likes.Any(l => l.UserId == meId.Value),
            p.Comments.Count
        ));

        return Ok(dtos);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromForm] string content, IFormFile? image)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
            return BadRequest(new { error = "Пост не может быть пустым или длиннее 2000 символов" });

        string? imagePath = null;
        if (image is not null)
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
                return BadRequest(new { error = "Допустимые форматы изображений: jpg, png, gif, webp" });
            if (image.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "Изображение не должно превышать 10 МБ" });

            var uploadsDir = Path.Combine("wwwroot", "uploads", "posts");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            await using var stream = System.IO.File.Create(filePath);
            await image.CopyToAsync(stream);
            imagePath = $"/uploads/posts/{fileName}";
        }

        var post = new Post
        {
            Content = content.Trim(),
            ImagePath = imagePath,
            UserId = CurrentUserId
        };

        db.Posts.Add(post);
        await db.SaveChangesAsync();
        await db.Entry(post).Reference(p => p.User).LoadAsync();

        var dto = new PostDto(
            post.Id, post.Content, post.CreatedAt,
            post.UserId, post.User.Username, post.User.DisplayName,
            GetFileUrl(post.User.AvatarPath), GetFileUrl(post.ImagePath),
            0, false, 0);

        await hub.Clients.All.SendAsync("NewPost", dto);
        return Ok(dto);
    }

    [HttpPost("{id}/like")]
    [Authorize]
    public async Task<IActionResult> Like(int id)
    {
        var meId = CurrentUserId;
        var post = await db.Posts.Include(p => p.Likes).FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        var existing = post.Likes.FirstOrDefault(l => l.UserId == meId);
        bool liked;
        if (existing is null)
        {
            db.PostLikes.Add(new PostLike { PostId = id, UserId = meId });
            liked = true;
        }
        else
        {
            db.PostLikes.Remove(existing);
            liked = false;
        }

        await db.SaveChangesAsync();
        var count = await db.PostLikes.CountAsync(l => l.PostId == id);

        await hub.Clients.All.SendAsync("UpdateLikes", new { postId = id, likesCount = count });

        return Ok(new { liked, likesCount = count });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await db.Posts.FindAsync(id);
        if (post is null) return NotFound();
        if (post.UserId != CurrentUserId) return Forbid();

        if (!string.IsNullOrEmpty(post.ImagePath))
        {
            var filePath = Path.Combine("wwwroot", post.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        db.Posts.Remove(post);
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("DeletePost", id);
        return NoContent();
    }
    
    public record CommentDto(
        int Id, string Content, DateTime CreatedAt,
        int UserId, string Username, string DisplayName,
        string? AvatarUrl);

    public record CreateCommentRequest(string Content);
    
    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(int id)
    {
        var exists = await db.Posts.AnyAsync(p => p.Id == id);
        if (!exists) return NotFound();

        var rows = await db.Comments
            .Include(c => c.User)
            .Where(c => c.PostId == id)
            .OrderBy(c => c.Id)
            .Select(c => new {
                c.Id, c.Content, c.CreatedAt,
                c.UserId,
                c.User.Username,
                c.User.DisplayName,
                c.User.AvatarPath
            })
            .ToListAsync();

        var dtos = rows.Select(c => new CommentDto(
            c.Id, c.Content, c.CreatedAt,
            c.UserId, c.Username, c.DisplayName,
            GetFileUrl(c.AvatarPath)));

        return Ok(dtos);
    }
    
    [HttpPost("{id}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(int id, CreateCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content) || req.Content.Length > 1000)
            return BadRequest(new { error = "Комментарий не может быть пустым или длиннее 1000 символов" });

        var post = await db.Posts.FindAsync(id);
        if (post is null) return NotFound();

        var comment = new Comment
        {
            Content = req.Content.Trim(),
            PostId = id,
            UserId = CurrentUserId
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        await db.Entry(comment).Reference(c => c.User).LoadAsync();

        var dto = new CommentDto(
            comment.Id, comment.Content, comment.CreatedAt,
            comment.UserId, comment.User.Username, comment.User.DisplayName,
            GetFileUrl(comment.User.AvatarPath));

        await hub.Clients.All.SendAsync("NewComment", new { postId = id, comment = dto });

        return Ok(dto);
    }

    [HttpDelete("{id}/comments/{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int id, int commentId)
    {
        var comment = await db.Comments.FindAsync(commentId);
        if (comment is null) return NotFound();
        if (comment.PostId != id) return BadRequest();
        if (comment.UserId != CurrentUserId) return Forbid();

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
    }
}