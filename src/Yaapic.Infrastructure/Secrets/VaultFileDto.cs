using System.Text.Json.Serialization;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>The vault file, as described in ADR 0010.</summary>
internal sealed class VaultFileDto
{
    public int Version { get; set; }

    public string Protector { get; set; } = "";

    public string WrappedKey { get; set; } = "";

    public VaultKdfDto? Kdf { get; set; }

    public List<VaultEntryDto> Entries { get; set; } = [];
}

/// <summary>The key derivation parameters of a passphrase vault.</summary>
internal sealed class VaultKdfDto
{
    public string Algorithm { get; set; } = "";

    public int Iterations { get; set; }

    public string Salt { get; set; } = "";
}

/// <summary>One encrypted entry of the vault file.</summary>
internal sealed class VaultEntryDto
{
    public string Kind { get; set; } = "";

    public string Owner { get; set; } = "";

    public string Name { get; set; } = "";

    public string Nonce { get; set; } = "";

    public string Ciphertext { get; set; } = "";

    public string Tag { get; set; } = "";
}

/// <summary>Source-generated JSON metadata for the vault file (Native AOT, no reflection).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(VaultFileDto))]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
