using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LiveStock.Bot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string StockCommandsQueue = "stock-commands";
    private const string StockResponsesQueue = "stock-responses";

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(StockCommandsQueue, false, false, false, null, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(StockResponsesQueue, false, false, false, null, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var stockCode = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Received stock command: {StockCode}", stockCode);

            try
            {
                var response = await GetStockQuoteAsync(stockCode);
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await _channel.BasicPublishAsync(string.Empty, StockResponsesQueue, responseBytes, stoppingToken);
                _logger.LogInformation("Published response: {Response}", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stock command: {StockCode}", stockCode);
                var errorMessage = $"Error getting quote for {stockCode}";
                var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                await _channel.BasicPublishAsync(string.Empty, StockResponsesQueue, errorBytes, stoppingToken);
            }

            await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
        };

        await _channel.BasicConsumeAsync(StockCommandsQueue, false, consumer, stoppingToken);

        _logger.LogInformation("Bot started, waiting for stock commands...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<string> GetStockQuoteAsync(string stockCode)
    {
        var url = $"https://stooq.com/q/l/?s={stockCode}&f=sd2t2ohlcv&h&e=csv";
        var csv = await _httpClient.GetStringAsync(url);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return $"No data found for {stockCode}";

        var values = lines[1].Split(',');
        if (values.Length < 7)
            return $"Invalid data for {stockCode}";

        var symbol = values[0];
        var close = values[6];

        if (close == "N/D" || string.IsNullOrWhiteSpace(close))
            return $"{stockCode.ToUpperInvariant()} quote is not available";

        return $"{symbol.ToUpperInvariant()} quote is ${close} per share";
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
