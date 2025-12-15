namespace LiveStock.Web.Services;

public interface IRabbitMqService
{
    Task PublishStockCommandAsync(string stockCode);
    Task InitializeAsync();
}
