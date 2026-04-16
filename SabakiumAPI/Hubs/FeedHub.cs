using Microsoft.AspNetCore.SignalR;

namespace SabakiumAPI.Hubs;

public class FeedHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
