using System.Text.Json;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderItemReserver
{
    public class OrderItemFunc
    {
        private readonly ILogger _logger;
        private const string BlobConnection = "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING";
        private const string BlobContainer = "order-item-container";
        private const string LogicApp = "LOGIC_APP_TRIGGER";

        public OrderItemFunc(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<OrderItemFunc>();
        }

        [Function("OrderItemFunc")]
        public async Task Run([ServiceBusTrigger("orders", Connection = "OrderItemsQueueConnection")] string orderItemReserve)
        {
            try
            {
                var binaryData = new BinaryData(orderItemReserve);

                var blobOptions = new BlobClientOptions
                {
                    Retry = {
                        Delay = TimeSpan.FromSeconds(2),
                        MaxRetries = 3,
                        Mode = RetryMode.Exponential,
                        MaxDelay = TimeSpan.FromSeconds(10),
                        NetworkTimeout = TimeSpan.FromSeconds(100)
                    },
                };
                BlobServiceClient blobServiceClient = new(Environment.GetEnvironmentVariable(BlobConnection), blobOptions);
                var containerClient = blobServiceClient.GetBlobContainerClient(BlobContainer);
                await containerClient.CreateIfNotExistsAsync();
                await containerClient.UploadBlobAsync(Path.ChangeExtension(Guid.NewGuid().ToString(), "json"), binaryData);
            }
            catch (Exception ex)
            {
                var content = JsonSerializer.Serialize(new
                {
                    To = "mariia_kim@epam.com",
                    Body = $"Order details received data: {orderItemReserve}\nException Message: {ex.Message}"
                });
                using var httpClient = new HttpClient();
                await httpClient.PostAsync(Environment.GetEnvironmentVariable(LogicApp), new StringContent(content));
            }
        }
    }
}
