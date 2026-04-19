using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SabakiumAPI.Hubs;

[Authorize]
public class CallHub : Hub
{
    private static readonly ConcurrentDictionary<int, string> _connections = new();

    private int Me => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string MyName => Context.User!.FindFirstValue(ClaimTypes.Name)
                             ?? Context.User!.FindFirstValue("displayName")
                             ?? "Пользователь";

    public override Task OnConnectedAsync()
    {
        _connections[Me] = Context.ConnectionId;
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _connections.TryRemove(Me, out _);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendCallOffer(int recipientId, string offerJson)
    {
        if (_connections.TryGetValue(recipientId, out var connId))
            await Clients.Client(connId).SendAsync("CallOffer", Me, MyName, offerJson);
    }

    public async Task SendCallAnswer(int callerId, string answerJson)
    {
        if (_connections.TryGetValue(callerId, out var connId))
            await Clients.Client(connId).SendAsync("CallAnswer", answerJson);
    }

    public async Task SendIceCandidate(int targetId, string candidateJson)
    {
        if (_connections.TryGetValue(targetId, out var connId))
            await Clients.Client(connId).SendAsync("IceCandidate", candidateJson);
    }

    public async Task SendCallEnd(int targetId)
    {
        if (_connections.TryGetValue(targetId, out var connId))
            await Clients.Client(connId).SendAsync("CallEnd");
    }
}