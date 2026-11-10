using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Maps <see cref="KeyValueEntry"/> to and from <see cref="KeyValueDto"/>; only variable lists carry the secret flag.</summary>
internal static class KeyValueMapper
{
    /// <summary>Maps an entry to its DTO.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="allowSecret">Whether the list is a variable list; if so, <c>secret: true</c> is written for secret rows only.</param>
    /// <returns>The DTO.</returns>
    public static KeyValueDto ToDto(KeyValueEntry entry, bool allowSecret) => new()
    {
        Key = entry.Key,
        Value = entry.Value,
        Enabled = entry.Enabled,
        Secret = allowSecret && entry.IsSecret ? true : null,
    };

    /// <summary>Maps a DTO to an entry; a missing or <c>false</c> flag, or a list that disallows it, is not secret.</summary>
    /// <param name="dto">The DTO.</param>
    /// <param name="allowSecret">Whether the list is a variable list.</param>
    /// <returns>The entry.</returns>
    public static KeyValueEntry FromDto(KeyValueDto dto, bool allowSecret) =>
        new(dto.Key ?? "", dto.Value ?? "", dto.Enabled ?? true, allowSecret && dto.Secret == true);

    /// <summary>
    /// Drops the <c>secret: null</c> lines VYaml writes for non-secret rows, so their output stays identical to milestone 2.
    /// </summary>
    /// <param name="yaml">The serialized document with <c>\n</c> line endings.</param>
    /// <returns>The document without the null secret lines.</returns>
    /// <remarks>
    /// A line of exactly this text cannot belong to a block scalar: block content is indented deeper than its key.
    /// </remarks>
    public static string StripNullSecret(string yaml) => yaml.Replace("\n  secret: null", "");
}
