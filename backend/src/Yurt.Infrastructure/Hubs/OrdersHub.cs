using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Hubs;

[Authorize]
public class OrdersHub : Hub
{
    private readonly IApplicationDbContext _db;

    public OrdersHub(IApplicationDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userType = Context.User?.FindFirstValue("user_type");

        if (userId == null) return;

        if (userType == "customer")
        {
            // Customers join their personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"customer:{userId}");
        }
        else if (userType == "admin")
        {
            // Admins join the global admin group to receive all order events
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }

        await base.OnConnectedAsync();
    }

    // Admins call this to subscribe to a location's order stream
    public async Task SubscribeToLocation(string locationId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "admin") return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"location:{locationId}");
    }

    public async Task UnsubscribeFromLocation(string locationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"location:{locationId}");
    }

    public async Task JoinGroupCart(string groupCartId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "customer") return;

        // Only members of the group cart may listen to its updates.
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(groupCartId, out var cartId) || !Guid.TryParse(userId, out var customerId)) return;
        var isMember = await _db.GroupCartMembers
            .AnyAsync(m => m.GroupCartId == cartId && m.CustomerUserId == customerId);
        if (!isMember) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"group-{groupCartId}");
    }

    public async Task LeaveGroupCart(string groupCartId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-{groupCartId}");
}
