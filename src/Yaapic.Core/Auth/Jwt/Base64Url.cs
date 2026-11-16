namespace Yaapic.Core.Auth.Jwt;

/// <summary>Base64url encoding without padding (RFC 4648 section 5).</summary>
public static class Base64Url
{
    /// <summary>Encodes bytes as unpadded base64url text.</summary>
    /// <param name="data">The bytes to encode.</param>
    /// <returns>The base64url text.</returns>
    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes base64url text, with or without padding.</summary>
    /// <param name="text">The base64url text.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="FormatException">The text is not valid base64url.</exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimEnd('=');
        if (text.Length - trimmed.Length > 2 || trimmed.AsSpan().ContainsAny('+', '/', '='))
        {
            throw new FormatException("Invalid base64url character.");
        }

        var s = trimmed.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            0 => s,
            2 => s + "==",
            3 => s + "=",
            _ => throw new FormatException("Invalid base64url length."),
        };
        return Convert.FromBase64String(s);
    }
}
