using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;

namespace SabakiumAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(AppDbContext db) : ControllerBase
{
    private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
            .Select(m => new
            {
                id                = m.Id,
                senderId          = m.SenderId,
                senderUsername    = m.Sender.Username,
                senderDisplayName = m.Sender.DisplayName,
                recipientId       = m.RecipientId,
                ciphertext        = m.Ciphertext,
                iv                = m.Iv,
                authTag           = m.AuthTag,
                createdAt         = m.CreatedAt,
            })
            .ToListAsync();

        return Ok(msgs);
    }

    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? q)
    {
        var me = Me;
        var query = db.Users.Where(u => u.Id != me);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Username.StartsWith(q) || u.DisplayName.Contains(q));

        var users = await query
            .OrderBy(u => u.Username)
            .Take(30)
            .Select(u => new { id = u.Id, username = u.Username, displayName = u.DisplayName })
            .ToListAsync();

        return Ok(users);
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
                .Include(m => m.Recipient)
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
}