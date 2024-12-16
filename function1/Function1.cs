using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FunctionAppTest
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _serviceBusConnectionString;
        private readonly string _queueName;

        public Function1(ILogger<Function1> logger, IConfiguration configuration)
        {
            _logger = logger;
            _serviceBusConnectionString = configuration["ServiceBusConnectionString"];
            _queueName = configuration["QueueName"];
        }

        [Function(nameof(Function1))]
        public async Task Run(
            [BlobTrigger("%ContainerName%/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream, 
            string name)
        {
            _logger.LogInformation($"Blob trigger function started processing the blob: {name}");

            try
            {
                await SendMessageToServiceBusAsync(name);
                _logger.LogInformation($"Message for blob '{name}' sent to Service Bus queue '{_queueName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the blob '{name}': {ex.Message}");
            }
        }

        private async Task SendMessageToServiceBusAsync(string messageContent)
        {
            await using var client = new ServiceBusClient(_serviceBusConnectionString);
            ServiceBusSender sender = client.CreateSender(_queueName);

            try
            {
                ServiceBusMessage message = new ServiceBusMessage(messageContent);
                await sender.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while sending message to Service Bus: {ex.Message}");
                throw;
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }
    }
}
