using System.Text;
using System.Text.Json;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>
/// The encrypted per-user, per-workspace vault file (ADR 0010). Reads (<see cref="TryGet"/>, <see cref="GetSecretValues"/>)
/// use an immutable snapshot swapped after each successful write; writes are serialised and atomic.
/// </summary>
/// <param name="workspaceRoot">The workspace root directory, which holds the workspace id.</param>
/// <param name="location">Where the vault file lives.</param>
/// <param name="protectors">The key protectors.</param>
public sealed class FileSecretVault(string workspaceRoot, IVaultLocation location, IKeyProtectorFactory protectors) : ISecretVault
{
    private const int CurrentVersion = 1;
    private static readonly IReadOnlyDictionary<SecretRef, string> NoSecrets = new Dictionary<SecretRef, string>();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _path;
    private string? _workspaceId;
    private WrappedKey? _wrapped;
    private Dictionary<SecretRef, EncryptedSecret> _entries = [];
    private byte[]? _dataKey;
    private volatile IReadOnlyDictionary<SecretRef, string> _cache = NoSecrets;
    private volatile IReadOnlyList<string> _values = [];
    private volatile IReadOnlyList<string> _warnings = [];
    private volatile VaultState _state = VaultState.NotCreated;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public VaultState State => _state;

    /// <inheritdoc />
    public string? ProtectorId { get; private set; }

    /// <summary>Gets the entries skipped by the last read (unreadable or tampered), by name; never values.</summary>
    public IReadOnlyList<string> LoadWarnings => _warnings;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            ClearKey();
            var path = ResolvePath();
            if (!File.Exists(path))
            {
                ProtectorId = null;
                _wrapped = null;
                _entries = [];
                SetState(VaultState.NotCreated);
                return;
            }

            try
            {
                Parse(await File.ReadAllTextAsync(path, ct));
            }
            catch (IOException ex)
            {
                SetState(VaultState.Locked);
                throw new VaultException($"Cannot read the vault: {ex.Message}", ex);
            }
            catch (VaultException)
            {
                SetState(VaultState.Locked);
                throw;
            }

            SetState(VaultState.Locked);
            var protector = protectors.Create(ProtectorId!, null);
            if (protector is null || protector.RequiresPassphrase)
            {
                return;
            }

            try
            {
                var key = await Task.Run(() => protector.Unwrap(_wrapped!, _workspaceId!), ct);
                ApplyKey(key);
            }
            catch (VaultException)
            {
                // Stays Locked: the UI explains that the protector could not unlock the vault.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = ResolvePath();
            if (File.Exists(path))
            {
                throw new VaultException("The vault already exists");
            }

            var protector = protectors.Create(protectorId, passphrase)
                ?? throw new VaultException($"The protector '{protectorId}' is not available");
            if (!protector.IsAvailable(out var reason))
            {
                throw new VaultException(reason ?? $"The protector '{protectorId}' is not available");
            }

            if (protector.RequiresPassphrase && string.IsNullOrEmpty(passphrase))
            {
                throw new VaultException("A passphrase is required");
            }

            var key = DataKey.Generate();
            try
            {
                var wrapped = await Task.Run(() => protector.Wrap(key, _workspaceId!), ct);
                await WriteAsync(path, protector.Id, wrapped, [], ct);
                ClearKey();
                _dataKey = key;
                ProtectorId = protector.Id;
                _wrapped = wrapped;
                _entries = [];
                _warnings = [];
                Publish(NoSecrets);
                SetState(VaultState.Unlocked);
            }
            catch
            {
                if (!ReferenceEquals(_dataKey, key))
                {
                    DataKey.Clear(key);
                }

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnlockAsync(string passphrase, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_state == VaultState.Unlocked)
            {
                return true;
            }

            if (_state == VaultState.NotCreated || string.IsNullOrEmpty(passphrase))
            {
                return false;
            }

            var protector = protectors.Create(ProtectorId!, passphrase);
            if (protector is null)
            {
                return false;
            }

            try
            {
                ApplyKey(await Task.Run(() => protector.Unwrap(_wrapped!, _workspaceId!), ct));
                return true;
            }
            catch (VaultException)
            {
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Lock()
    {
        _gate.Wait();
        try
        {
            if (_state != VaultState.Unlocked)
            {
                return;
            }

            ClearKey();
            SetState(VaultState.Locked);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public bool TryGet(SecretRef secret, out string value)
    {
        if (_cache.TryGetValue(secret, out var found))
        {
            value = found;
            return true;
        }

        value = "";
        return false;
    }

    /// <inheritdoc />
    public Task SetAsync(SecretRef secret, string value, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(value);
        return MutateAsync(
            (key, entries, cache) =>
            {
                entries[secret] = SecretCipher.Encrypt(key, secret, value);
                cache[secret] = value;
            },
            ct);
    }

    /// <inheritdoc />
    public Task RemoveAsync(SecretRef secret, CancellationToken ct) =>
        MutateAsync(
            (_, entries, cache) =>
            {
                entries.Remove(secret);
                cache.Remove(secret);
            },
            ct);

    /// <inheritdoc />
    public Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken ct) =>
        MutateAsync(
            (_, entries, cache) =>
            {
                foreach (var key in entries.Keys.Where(k => k.Kind == kind && k.OwnerId == ownerId).ToList())
                {
                    entries.Remove(key);
                    cache.Remove(key);
                }
            },
            ct);

    /// <inheritdoc />
    public IReadOnlyList<string> GetSecretValues() => _values;

    private string ResolvePath()
    {
        _workspaceId ??= Workspace.ReadWorkspaceId(workspaceRoot);
        return _path ??= location.GetVaultPath(_workspaceId);
    }

    private async Task MutateAsync(Action<byte[], Dictionary<SecretRef, EncryptedSecret>, Dictionary<SecretRef, string>> change, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_state != VaultState.Unlocked || _dataKey is null)
            {
                throw new VaultException(_state == VaultState.NotCreated ? "The vault does not exist" : "The vault is locked");
            }

            var entries = new Dictionary<SecretRef, EncryptedSecret>(_entries);
            var cache = new Dictionary<SecretRef, string>(_cache);
            change(_dataKey, entries, cache);
            await WriteAsync(_path!, ProtectorId!, _wrapped!, entries, ct);
            _entries = entries;
            Publish(cache);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Parse(string json)
    {
        VaultFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFileDto);
        }
        catch (JsonException ex)
        {
            throw new VaultException("The vault file is corrupt", ex);
        }

        if (dto is null)
        {
            throw new VaultException("The vault file is corrupt");
        }

        if (dto.Version != CurrentVersion)
        {
            throw new VaultException("Unsupported vault version");
        }

        try
        {
            var kdf = dto.Kdf is null ? null : new VaultKdf(dto.Kdf.Algorithm, dto.Kdf.Iterations, Convert.FromBase64String(dto.Kdf.Salt));
            _wrapped = new WrappedKey(Convert.FromBase64String(dto.WrappedKey), kdf);
        }
        catch (FormatException ex)
        {
            throw new VaultException("The vault file is corrupt", ex);
        }

        ProtectorId = dto.Protector;
        var entries = new Dictionary<SecretRef, EncryptedSecret>();
        var warnings = new List<string>();
        foreach (var entry in dto.Entries ?? [])
        {
            if (TryParseEntry(entry, out var secret, out var encrypted))
            {
                entries[secret] = encrypted;
            }
            else
            {
                warnings.Add($"The vault entry '{entry.Name}' is malformed and was skipped");
            }
        }

        _entries = entries;
        _warnings = warnings;
    }

    private static bool TryParseEntry(VaultEntryDto entry, out SecretRef secret, out EncryptedSecret encrypted)
    {
        secret = default;
        encrypted = null!;
        if (!Enum.TryParse(entry.Kind, out SecretOwnerKind kind) || !Enum.IsDefined(kind)
            || !Guid.TryParseExact(entry.Owner, "D", out var owner) || string.IsNullOrEmpty(entry.Name))
        {
            return false;
        }

        try
        {
            encrypted = new EncryptedSecret(
                Convert.FromBase64String(entry.Nonce),
                Convert.FromBase64String(entry.Ciphertext),
                Convert.FromBase64String(entry.Tag));
        }
        catch (FormatException)
        {
            return false;
        }

        secret = new SecretRef(kind, owner, entry.Name);
        return true;
    }

    private void ApplyKey(byte[] key)
    {
        var cache = new Dictionary<SecretRef, string>();
        var warnings = _warnings.ToList();
        foreach (var (secret, encrypted) in _entries)
        {
            try
            {
                cache[secret] = SecretCipher.Decrypt(key, secret, encrypted);
            }
            catch (VaultException)
            {
                warnings.Add($"The vault entry '{secret.Name}' cannot be decrypted and was skipped");
            }
        }

        ClearKey();
        _dataKey = key;
        _warnings = warnings;
        Publish(cache);
        SetState(VaultState.Unlocked);
    }

    private void ClearKey()
    {
        if (_dataKey is not null)
        {
            DataKey.Clear(_dataKey);
            _dataKey = null;
        }

        Publish(NoSecrets);
    }

    private void Publish(IReadOnlyDictionary<SecretRef, string> cache)
    {
        _values = [.. cache.Values.Where(v => v.Length >= 4).Distinct().OrderByDescending(v => v.Length)];
        _cache = cache;
    }

    private void SetState(VaultState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task WriteAsync(string path, string protectorId, WrappedKey wrapped, Dictionary<SecretRef, EncryptedSecret> entries, CancellationToken ct)
    {
        var dto = new VaultFileDto
        {
            Version = CurrentVersion,
            Protector = protectorId,
            WrappedKey = Convert.ToBase64String(wrapped.Bytes),
            Kdf = wrapped.Kdf is null
                ? null
                : new VaultKdfDto
                {
                    Algorithm = wrapped.Kdf.Algorithm,
                    Iterations = wrapped.Kdf.Iterations,
                    Salt = Convert.ToBase64String(wrapped.Kdf.Salt),
                },
            Entries =
            [
                .. entries.Select(e => new VaultEntryDto
                {
                    Kind = e.Key.Kind.ToString(),
                    Owner = e.Key.OwnerId.ToString("D"),
                    Name = e.Key.Name,
                    Nonce = Convert.ToBase64String(e.Value.Nonce),
                    Ciphertext = Convert.ToBase64String(e.Value.Ciphertext),
                    Tag = Convert.ToBase64String(e.Value.Tag),
                }),
            ],
        };
        var json = JsonSerializer.Serialize(dto, VaultJsonContext.Default.VaultFileDto);
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var tmp = path + ".tmp";
            await using (var stream = new FileStream(tmp, new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous,
            }))
            {
                await stream.WriteAsync(new UTF8Encoding(false).GetBytes(json), ct);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VaultException($"Cannot write the vault: {ex.Message}", ex);
        }
    }
}
