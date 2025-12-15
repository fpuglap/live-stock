using System.Text;
using RabbitMQ.Client;

namespace LiveStock.Web.Services;

public class RabbitMqService : IRabbitMqService, IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;
    private const string StockCommandsQueue = "stock-commands";

    public async Task InitializeAsync()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: StockCommandsQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    public async Task PublishStockCommandAsync(string stockCode)
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ not initialized");

        var body = Encoding.UTF8.GetBytes(stockCode);
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: StockCommandsQueue,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
    }
}
