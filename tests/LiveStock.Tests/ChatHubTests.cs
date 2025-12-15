using System.Security.Claims;
using LiveStock.Web.Data;
using LiveStock.Web.Hubs;
using LiveStock.Web.Models;
using LiveStock.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LiveStock.Tests;

public class ChatHubTests
{
    private static ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ChatHub hub, IRabbitMqService rabbitMq, IHubCallerClients clients) CreateHub(ApplicationDbContext db, string userName = "TestUser")
    {
        var rabbitMq = Substitute.For<IRabbitMqService>();
        var clients = Substitute.For<IHubCallerClients>();
        var clientProxy = Substitute.For<IClientProxy>();

        clients.All.Returns(clientProxy);

        var hub = new ChatHub(db, rabbitMq);

        var context = Substitute.For<HubCallerContext>();
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, userName)
        }, "test"));
        context.User.Returns(claims);

        hub.Clients = clients;
        hub.Context = context;

        return (hub, rabbitMq, clients);
    }

    [Fact]
    public async Task SendMessage_RegularMessage_SavesToDatabaseAndBroadcasts()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var (hub, _, clients) = CreateHub(db, "Alice");

        // Act
        await hub.SendMessage("Hello world");

        // Assert
        var messages = await db.ChatMessages.ToListAsync();
        Assert.Single(messages);
        Assert.Equal("Alice", messages[0].User);
        Assert.Equal("Hello world", messages[0].Message);

        _ = clients.Received(1).All;
    }

    [Fact]
    public async Task SendMessage_StockCommand_PublishesToRabbitMqAndDoesNotSaveToDb()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var (hub, rabbitMq, clients) = CreateHub(db);

        // Act
        await hub.SendMessage("/stock=AAPL");

        // Assert
        await rabbitMq.Received(1).PublishStockCommandAsync("AAPL");

        var messages = await db.ChatMessages.ToListAsync();
        Assert.Empty(messages);

        _ = clients.DidNotReceive().All;
    }

    [Fact]
    public async Task SendMessage_StockCommandCaseInsensitive_PublishesToRabbitMq()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var (hub, rabbitMq, _) = CreateHub(db);

        // Act
        await hub.SendMessage("/STOCK=msft");

        // Assert
        await rabbitMq.Received(1).PublishStockCommandAsync("MSFT");
    }

    [Fact]
    public async Task SendMessage_StockCommandWithSpaces_TrimsAndUppercases()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var (hub, rabbitMq, _) = CreateHub(db);

        // Act
        await hub.SendMessage("/stock=  googl  ");

        // Assert
        await rabbitMq.Received(1).PublishStockCommandAsync("GOOGL");
    }

    [Fact]
    public async Task SendMessage_EmptyStockCode_DoesNotPublish()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var (hub, rabbitMq, _) = CreateHub(db);

        // Act
        await hub.SendMessage("/stock=");

        // Assert
        await rabbitMq.DidNotReceive().PublishStockCommandAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetRecentMessages_ReturnsLast50OrderedByTimestamp()
    {
        // Arrange
        using var db = CreateInMemoryDb();

        for (int i = 0; i < 60; i++)
        {
            db.ChatMessages.Add(new ChatMessage
            {
                User = "User",
                Message = $"Message {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var (hub, _, _) = CreateHub(db);

        // Act
        var messages = await hub.GetRecentMessages();

        // Assert
        Assert.Equal(50, messages.Count);
        Assert.Equal("Message 10", messages[0].Message);
        Assert.Equal("Message 59", messages[49].Message);

        for (int i = 1; i < messages.Count; i++)
        {
            Assert.True(messages[i].Timestamp >= messages[i - 1].Timestamp);
        }
    }

    [Fact]
    public async Task GetRecentMessages_LessThan50Messages_ReturnsAll()
    {
        // Arrange
        using var db = CreateInMemoryDb();

        for (int i = 0; i < 10; i++)
        {
            db.ChatMessages.Add(new ChatMessage
            {
                User = "User",
                Message = $"Message {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var (hub, _, _) = CreateHub(db);

        // Act
        var messages = await hub.GetRecentMessages();

        // Assert
        Assert.Equal(10, messages.Count);
    }
}
