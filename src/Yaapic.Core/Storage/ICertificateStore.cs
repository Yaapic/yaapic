using Yaapic.Domain.Http;

namespace Yaapic.Core.Storage;

/// <summary>Persists the list of client certificates of the current user.</summary>
public interface ICertificateStore
{
    /// <summary>Raised after a successful <see cref="SaveAsync"/>.</summary>
    event EventHandler? Changed;

    /// <summary>Loads the list; empty when nothing was saved yet.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The certificate entries.</returns>
    /// <exception cref="StorageException">The file is corrupt or has an unknown version.</exception>
    Task<IReadOnlyList<ClientCertificate>> LoadAsync(CancellationToken ct);

    /// <summary>Replaces the stored list.</summary>
    /// <param name="certificates">The entries to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the list is stored.</returns>
    /// <exception cref="StorageException">The list cannot be written.</exception>
    Task SaveAsync(IReadOnlyList<ClientCertificate> certificates, CancellationToken ct);
}
