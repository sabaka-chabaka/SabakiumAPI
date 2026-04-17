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

    [HttpGet("history/{othetUserId:int}")]
    public async Task<IActionResult> History(int otherUserId, int? before, int limit = 50)
    {
        var me = Me;
        
        var q = db.ChatMessages
            .Where(m =>
                (m.SenderId == me && m.RecipientId == otherUserId) ||
                (m.SenderId == otherUserId && m.RecipientId == me))
            .OrderByDescending(m => m.Id);
 
        if (before.HasValue)
            q = (IOrderedQueryable<Models.ChatMessage>)q.Where(m => m.Id < before.Value);
 
        var msgs = await q.Take(limit)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                SenderUsername   = m.Sender.Username,
                SenderDisplayName= m.Sender.DisplayName,
                m.RecipientId,
                m.Ciphertext,
                m.Iv,
                m.AuthTag,
                m.CreatedAt
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
        
        var users = await query.OrderBy(u => u.Username).Take(30).Select(u => new { u.Id, u.Username, u.DisplayName }).ToListAsync();
        
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
                .Where(m =>
                    (m.SenderId == me && m.RecipientId == pid) ||
                    (m.SenderId == pid && m.RecipientId == me))
                .OrderByDescending(m => m.Id)
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync();
 
            if (latest == null) continue;
 
            var partner = await db.Users.FindAsync(pid);
            if (partner == null) continue;
 
            convs.Add(new
            {
                PartnerId          = partner.Id,
                PartnerUsername    = partner.Username,
                PartnerDisplayName = partner.DisplayName,
                LatestMessageId    = latest.Id,
                LatestCiphertext   = latest.Ciphertext,
                LatestIv           = latest.Iv,
                LatestAuthTag      = latest.AuthTag,
                LatestSenderId     = latest.SenderId,
                LatestCreatedAt    = latest.CreatedAt,
            });
        }
 
        return Ok(convs.OrderByDescending(c => ((dynamic)c).LatestMessageId));
    }
}