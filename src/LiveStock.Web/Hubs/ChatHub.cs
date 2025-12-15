using LiveStock.Web.Data;
using LiveStock.Web.Models;
using LiveStock.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveStock.Web.Hubs;

[Authorize]
public class ChatHub(ApplicationDbContext db, IRabbitMqService rabbitMq) : Hub
{
    public async Task SendMessage(string message)
    {
        var user = Context.User?.Identity?.Name ?? "Anonymous";
        var timestamp = DateTime.UtcNow;

        // Check if it's a stock command
        if (message.StartsWith("/stock=", StringComparison.OrdinalIgnoreCase))
        {
            var stockCode = message[7..].Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(stockCode))
            {
                await rabbitMq.PublishStockCommandAsync(stockCode);
            }
            return; // Don't save stock commands to DB
        }

        var chatMessage = new ChatMessage
        {
            User = user,
            Message = message,
            Timestamp = timestamp
        };

        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync();

        await Clients.All.SendAsync("ReceiveMessage", user, message, timestamp);
    }

    public async Task<List<ChatMessage>> GetRecentMessages()
    {
        return await db.ChatMessages
            .OrderByDescending(m => m.Timestamp)
            .Take(50)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }
}
