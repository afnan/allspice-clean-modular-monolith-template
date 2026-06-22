using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AllSpice.CleanModularMonolith.SharedKernel.Storage;

/// <summary>Implements <see cref="IFileStorageService"/> over an Azure Blob Storage container.</summary>
public sealed class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;
    private volatile bool _containerEnsured;

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, string containerName)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadAsync(Guid scopeId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        var safeFileName = Path.GetFileName(fileName);
        var blobName = $"{scopeId}/{Guid.NewGuid()}_{safeFileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
        return blobName;
    }

    public async Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);
        var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream, cancellationToken);
        stream.Position = 0;
        return stream;
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        _containerEnsured = true;
    }
}
