using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace AllSpice.CleanModularMonolith.SharedKernel.Storage;

public static class FileStorageServiceExtensions
{
    private static BlobServiceClient? _sharedClient;
    private static readonly object Lock = new();

    /// <summary>
    /// Creates an <see cref="IFileStorageService"/> backed by Azure Blob Storage for the given container.
    /// A single <see cref="BlobServiceClient"/> is shared across all modules for efficient connection
    /// pooling. Requires the <c>BlobConnection</c> connection string (provisioned by the AppHost via the
    /// Azure Storage / Azurite resource). Call from a module that needs file storage, e.g.
    /// <c>builder.Services.AddSingleton(_ =&gt; FileStorageServiceExtensions.CreateFileStorageService(builder.Configuration, "invoices"));</c>
    /// </summary>
    public static IFileStorageService CreateFileStorageService(IConfiguration configuration, string containerName)
    {
        var blobConnectionString = configuration.GetConnectionString("BlobConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'BlobConnection' is required. Provision Azure Blob Storage (or the Azurite emulator) on the gateway via the AppHost.");

        var client = GetOrCreateBlobServiceClient(blobConnectionString);
        return new AzureBlobStorageService(client, containerName);
    }

    private static BlobServiceClient GetOrCreateBlobServiceClient(string connectionString)
    {
        if (_sharedClient is not null)
        {
            return _sharedClient;
        }

        lock (Lock)
        {
            _sharedClient ??= new BlobServiceClient(connectionString);
        }

        return _sharedClient;
    }
}
