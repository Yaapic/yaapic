namespace Yaapic.Core.Secrets;

/// <summary>
/// Replaces the values held by the vault with a mask. Values are matched as written (ordinal, case-sensitive):
/// a secret that appears base64- or URL-encoded is not detected (ADR 0010 accepted limits).
/// </summary>
/// <param name="vault">The secret vault; a locked vault yields no values, so nothing is replaced.</param>
public sealed class SecretRedactor(ISecretVault vault) : ISecretRedactor
{
    private const string Mask = "••••";

    /// <inheritdoc/>
    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var values = vault.GetSecretValues();

        // Longest first (vault contract) so a secret containing a shorter one is replaced whole.
        foreach (var value in values)
        {
            text = text.Replace(value, Mask, StringComparison.Ordinal);
        }

        return text;
    }
}
