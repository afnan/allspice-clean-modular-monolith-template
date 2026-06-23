namespace AllSpice.CleanModularMonolith.SharedKernel.Storage;

/// <summary>
/// Abstraction for binary file upload/download (documents, logos, generated reports, etc.),
/// shared across modules. Implementations partition stored objects by a caller-supplied scope id.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads <paramref name="content"/> under the given <paramref name="scopeId"/> partition and
    /// returns the opaque storage path that can later be passed to <see cref="DownloadAsync"/>.
    /// </summary>
    Task<string> UploadAsync(Guid scopeId, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Downloads the object previously stored at <paramref name="storagePath"/>.</summary>
    Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default);
}
