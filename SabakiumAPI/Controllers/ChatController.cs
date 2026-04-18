using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;
using SabakiumAPI.Hubs;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(AppDbContext db, IHttpContextAccessor http) : ControllerBase
{
    private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string? BuildAvatarUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var req = http.HttpContext!.Request;
        return $"{req.Scheme}://{req.Host}{path}";
    }

    [HttpGet("history/{otherUserId:int}")]
    public async Task<IActionResult> History(int otherUserId, int? before, int limit = 50)
    {
        var me = Me;
        var q = db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m =>
                (m.SenderId == me && m.RecipientId == otherUserId) ||
                (m.SenderId == otherUserId && m.RecipientId == me));

        if (before.HasValue)
            q = q.Where(m => m.Id < before.Value);

        var msgs = await q
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(msgs.Select(m => new
        {
            id                = m.Id,
            senderId          = m.SenderId,
            senderUsername    = m.Sender.Username,
            senderDisplayName = m.Sender.DisplayName,
            senderAvatarUrl   = BuildAvatarUrl(m.Sender.AvatarPath),
            recipientId       = m.RecipientId,
            ciphertext        = m.Ciphertext,
            iv                = m.Iv,
            authTag           = m.AuthTag,
            createdAt         = m.CreatedAt,
        }));
    }

    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? q)
    {
        var me = Me;
        var query = db.Users.Where(u => u.Id != me);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Username.StartsWith(q) || u.DisplayName.Contains(q));

        var users = await query.OrderBy(u => u.Username).Take(30).ToListAsync();
        return Ok(users.Select(u => new
        {
            id          = u.Id,
            username    = u.Username,
            displayName = u.DisplayName,
            avatarUrl   = BuildAvatarUrl(u.AvatarPath),
        }));
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations()
    {
        var me = Me;
        var partnerIds = await db.ChatMessages
            .Where(m => m.SenderId == me || m.RecipientId == me)
            .Select(m => m.SenderId == me ? m.RecipientId : m.SenderId)
            .Distinct()
            .ToListAsync();

        var convs = new List<object>();
        foreach (var pid in partnerIds)
        {
            var latest = await db.ChatMessages
                .Include(m => m.Sender)
                .Where(m =>
                    (m.SenderId == me && m.RecipientId == pid) ||
                    (m.SenderId == pid && m.RecipientId == me))
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync();

            if (latest == null) continue;

            var partner = await db.Users.FindAsync(pid);
            if (partner == null) continue;

            convs.Add(new
            {
                partnerId          = partner.Id,
                partnerUsername    = partner.Username,
                partnerDisplayName = partner.DisplayName,
                partnerAvatarUrl   = BuildAvatarUrl(partner.AvatarPath),
                latestMessageId    = latest.Id,
                latestCiphertext   = latest.Ciphertext,
                latestIv           = latest.Iv,
                latestAuthTag      = latest.AuthTag,
                latestSenderId     = latest.SenderId,
                latestCreatedAt    = latest.CreatedAt,
            });
        }

        return Ok(convs.OrderByDescending(c => ((dynamic)c).latestMessageId));
    }

    [HttpGet("online/{userId}")]
    [Authorize]
    public IActionResult CheckOnline(int userId)
    {
        var online = UserConnections.Get(userId) != null;
        return Ok(new { online });
    }

    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file.Length > 50 * 1024 * 1024) return BadRequest(new { error = "Файл не должен превышать 50 МиБ" });
        
        var uploadsDir = Path.Combine("wwwroot", "uploads", "chat");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName);
        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);
        
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var req = HttpContext.Request;
        var url = $"{req.Scheme}://{req.Host}/uploads/chat/{safeFileName}";

        return Ok(new
        {
            url,
            fileName = file.FileName,
            fileSize = file.Length,
            mimeType = file.ContentType
        });
    }

    [HttpDelete("messages/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var msg = await db.ChatMessages.FindAsync(id);

        if (msg is null) return NotFound();
        if (msg.SenderId != userId) return Forbid();
        
        db.ChatMessages.Remove(msg);
        await db.SaveChangesAsync();
        return NoContent();
    }
}