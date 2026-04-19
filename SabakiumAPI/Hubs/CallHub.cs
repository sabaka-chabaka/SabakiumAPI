using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SabakiumAPI.Hubs;

[Authorize]
public class CallHub : Hub
{
    public async Task SendCallOffer(int recipientId, string offerJson)
    {
        var senderId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sender = UserConnections.GetName(senderId);
        var connId = UserConnections.Get(recipientId);
        if (connId != null)
            await Clients.Client(connId).SendAsync("ReceiveCallOffer", sender, offerJson);
    }
    
    public async Task SendCallAnswer(int callerId, string answerJson)
    {
        var connId = UserConnections.Get(callerId);
        if (connId != null)
            await Clients.Client(connId).SendAsync("CallAnswer", answerJson);
    }

    public async Task SendIceCandidate(int targetId, string candidateJson)
    {
        var connId = UserConnections.Get(targetId);
        if (connId != null)
            await Clients.Client(connId).SendAsync("IceCandidate", candidateJson);
    }

    public async Task SendCallEnd(int targetId)
    {
        var connId = UserConnections.Get(targetId);
        if (connId != null)
            await Clients.Client(connId).SendAsync("CallEnd");
    }
}