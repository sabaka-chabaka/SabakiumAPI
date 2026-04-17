using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Data;
using SabakiumAPI.Models;

namespace SabakiumAPI.Hubs;

[Authorize]
public class ChatHub(AppDbContext db) : Hub
{
    public record EncryptedMessageDto(
        int Id,
        int SenderId,
        string SenderUsername,
        string SenderDisplayName,
        int RecipientId,
        string Ciphertext,
        string Iv,
        string AuthTag,
        DateTime CreatedAt
    );

    public async Task SendMessage(int recipientId, string ciphertext, string iv, string authTag)
    {
        var senderId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var sender = await db.Users.FindAsync(senderId)
            ?? throw new HubException("Пользователь не найден");

        if (!await db.Users.AnyAsync(u => u.Id == recipientId))
            throw new HubException("Получатель не найден");

        var msg = new ChatMessage
        {
            SenderId = senderId,
            RecipientId = recipientId,
            Ciphertext = ciphertext,
            Iv = iv,
            AuthTag = authTag,
        };

        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new EncryptedMessageDto(
            msg.Id,
            msg.SenderId,
            sender.Username,
            sender.DisplayName,
            msg.RecipientId,
            msg.Ciphertext,
            msg.Iv,
            msg.AuthTag,
            msg.CreatedAt
        );

        var recipientConnId = UserConnections.Get(recipientId);
        if (recipientConnId != null)
            await Clients.Client(recipientConnId).SendAsync("ReceiveMessage", dto);

        await Clients.Caller.SendAsync("ReceiveMessage", dto);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        UserConnections.Add(userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        UserConnections.Remove(userId);
        await base.OnDisconnectedAsync(exception);
    }
}

public static class UserConnections
{
    private static readonly Dictionary<int, string> _map = new();
    private static readonly Lock _lock = new();

    public static void Add(int userId, string connId)
    {
        lock (_lock) _map[userId] = connId;
    }

    public static void Remove(int userId)
    {
        lock (_lock) _map.Remove(userId);
    }

    public static string? Get(int userId)
    {
        lock (_lock) return _map.TryGetValue(userId, out var c) ? c : null;
    }
}