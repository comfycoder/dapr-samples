using Azure.Core;
using Azure.Storage.Blobs;

public class BlobStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly TokenCredential _tokenCredential;
    private BlobContainerClient _containerClient;

    public BlobStorageService(
        IConfiguration configuration,
        ILogger<BlobStorageService> logger,
        TokenCredential tokenCredential)
    {
        _configuration = configuration;
        _logger = logger;
        _tokenCredential = tokenCredential;
    }

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        if (_containerClient != null)
        {
            return _containerClient;
        }

        string storageAccountName = _configuration["Storage:AccountName"];
        string containerName = _configuration["Storage:ContainerName"];

        // Create BlobServiceClient using managed identity
        var blobServiceClient = new BlobServiceClient(
            new Uri($"https://{storageAccountName}.blob.core.windows.net"),
            _tokenCredential);

        // Get container client
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Create container if it doesn't exist
        await _containerClient.CreateIfNotExistsAsync();

        return _containerClient;
    }

    public async Task UploadBlobAsync(string blobPath, Stream content)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(blobPath);

            await blobClient.UploadAsync(content, overwrite: true);
            _logger.LogInformation($"Uploaded blob: {blobPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading blob: {blobPath}");
            throw;
        }
    }

    public async Task<Stream> DownloadBlobAsync(string blobPath)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(blobPath);

            var downloadInfo = await blobClient.DownloadAsync();
            var memoryStream = new MemoryStream();
            await downloadInfo.Value.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error downloading blob: {blobPath}");
            throw;
        }
    }
}
