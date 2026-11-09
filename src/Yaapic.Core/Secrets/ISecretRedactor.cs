namespace Yaapic.Core.Secrets;

/// <summary>Scrubs secret values out of text.</summary>
public interface ISecretRedactor
{
    /// <summary>Replaces every known secret value in a text.</summary>
    /// <param name="text">The text; <see langword="null"/> or empty is returned unchanged.</param>
    /// <returns>The text with each secret value replaced by a mask.</returns>
    string Redact(string text);
}
