using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace QueueTriggerFunction
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Function1))]
        public async Task Run(
            [ServiceBusTrigger("%QueueName%", Connection = "ServiceBusConnectionString")] string message) // s'exécute des qu'un message arrive sur la queue
        {
            _logger.LogInformation($"Service Bus trigger function processed message: {message}");

            // Connection string and containers
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string sourceContainerName = Environment.GetEnvironmentVariable("ContainerName");
            string destinationContainerName = Environment.GetEnvironmentVariable("ContainerName2");

            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient sourceContainerClient = blobServiceClient.GetBlobContainerClient(sourceContainerName);
                BlobContainerClient destinationContainerClient = blobServiceClient.GetBlobContainerClient(destinationContainerName);

                BlobClient sourceBlobClient = sourceContainerClient.GetBlobClient(message);
                if (!await sourceBlobClient.ExistsAsync())
                {
                    _logger.LogError($"Blob '{message}' not found in source container '{sourceContainerName}'.");
                    return;
                }

                // On récupere localement l'image
                MemoryStream memoryStream = new MemoryStream();
                await sourceBlobClient.DownloadToAsync(memoryStream);

                // on resize l'image (divisé par deux seulement)
                memoryStream.Position = 0;
                using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(memoryStream))
                {
                    int newWidth = image.Width / 2;
                    int newHeight = image.Height / 2;

                    image.Mutate(x => x.Resize(newWidth, newHeight));

                    memoryStream = new MemoryStream(); // Reset the stream
                    image.Save(memoryStream, new JpegEncoder());
                }


                // ouvre le fichier au début
                memoryStream.Position = 0;
                BlobClient destinationBlobClient = destinationContainerClient.GetBlobClient(message); // destination de ma nouvelle image (finalcontainer)
                await destinationBlobClient.UploadAsync(memoryStream, overwrite: true);

                _logger.LogInformation($"File '{message}' successfully processed and uploaded to '{destinationContainerName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing blob '{message}': {ex.Message}");
            }
        }
    }
}
