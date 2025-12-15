using System.Text;
using LiveStock.Web.Data;
using LiveStock.Web.Hubs;
using LiveStock.Web.Models;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LiveStock.Web.Services;

public class StockResponseConsumer : BackgroundService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockResponseConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string StockResponsesQueue = "stock-responses";

    public StockResponseConsumer(IHubContext<ChatHub> hubContext, IServiceScopeFactory scopeFactory, ILogger<StockResponseConsumer> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(StockResponsesQueue, false, false, false, null, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var timestamp = DateTime.UtcNow;
            _logger.LogInformation("Received stock response: {Message}", message);

            // Save to database
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.ChatMessages.Add(new ChatMessage
                {
                    User = "StockBot",
                    Message = message,
                    Timestamp = timestamp
                });
                await db.SaveChangesAsync(stoppingToken);
            }

            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "StockBot", message, timestamp, stoppingToken);
            await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
        };

        await _channel.BasicConsumeAsync(StockResponsesQueue, false, consumer, stoppingToken);

        _logger.LogInformation("Stock response consumer started...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection != null)
            await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
