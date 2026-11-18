using System.Text.Json;
using System.Text.Json.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Secrets;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the client certificate list in <c>&lt;user data dir&gt;/Yaapic/certificates.json</c>: per user, outside every workspace.</summary>
/// <param name="userDataDirectory">Replaces the per-user data directory (tests, screenshot captures); <see langword="null"/> for the real one.</param>
public sealed class FileCertificateStore(string? userDataDirectory = null) : ICertificateStore
{
    private const int CurrentVersion = 1;

    private readonly string _path = Path.Combine(userDataDirectory ?? DefaultVaultLocation.UserDataDirectory(), "Yaapic", "certificates.json");

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientCertificate>> LoadAsync(CancellationToken ct)
    {
        string text;
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            text = await File.ReadAllTextAsync(_path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot read '{_path}': {ex.Message}", ex);
        }

        CertificatesFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(text, CertificatesJsonContext.Default.CertificatesFileDto);
        }
        catch (JsonException ex)
        {
            throw new StorageException($"The certificate list '{_path}' is not valid JSON: {ex.Message}", ex);
        }

        if (dto is null || dto.Version != CurrentVersion)
        {
            throw new StorageException($"The certificate list '{_path}' has an unsupported version ({dto?.Version.ToString() ?? "none"}); expected {CurrentVersion}.");
        }

        var result = new List<ClientCertificate>();
        foreach (var e in dto.Certificates ?? [])
        {
            if (Enum.TryParse<ClientCertificateFormat>(e.Format, ignoreCase: true, out var format) && Enum.IsDefined(format))
            {
                result.Add(new ClientCertificate(e.Id, e.Name ?? "", e.HostPattern ?? "", format, e.CertificatePath ?? "", e.KeyPath ?? "", e.HasPassphrase));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<ClientCertificate> certificates, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        var dto = new CertificatesFileDto
        {
            Version = CurrentVersion,
            Certificates = [.. certificates.Select(c => new CertificateDto
            {
                Id = c.Id,
                Name = c.Name,
                HostPattern = c.HostPattern,
                Format = c.Format.ToString().ToLowerInvariant(),
                CertificatePath = c.CertificatePath,
                KeyPath = c.KeyPath,
                HasPassphrase = c.HasPassphrase,
            })],
        };
        try
        {
            await WorkspaceFiles.WriteAtomicAsync(_path, JsonSerializer.Serialize(dto, CertificatesJsonContext.Default.CertificatesFileDto) + "\n", ct, userOnly: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write the certificate list: {ex.Message}", ex);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class CertificatesFileDto
{
    public int Version { get; set; }

    public List<CertificateDto>? Certificates { get; set; }
}

internal sealed class CertificateDto
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? HostPattern { get; set; }

    public string? Format { get; set; }

    public string? CertificatePath { get; set; }

    public string? KeyPath { get; set; }

    public bool HasPassphrase { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(CertificatesFileDto))]
internal partial class CertificatesJsonContext : JsonSerializerContext;
