using Yaapic.Core.Storage;
using Yaapic.Domain.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="ICertificateStore"/> kept in memory.</summary>
public sealed class InMemoryCertificateStore : ICertificateStore
{
    private IReadOnlyList<ClientCertificate> _items = [];

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ClientCertificate>> LoadAsync(CancellationToken ct) => Task.FromResult(_items);

    /// <inheritdoc/>
    public Task SaveAsync(IReadOnlyList<ClientCertificate> certificates, CancellationToken ct)
    {
        _items = [.. certificates];
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
