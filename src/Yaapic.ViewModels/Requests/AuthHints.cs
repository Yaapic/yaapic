using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>Helpers shared by the auth editors.</summary>
internal static class AuthHints
{
    /// <summary>Returns whether <paramref name="value"/> is a non-empty literal, i.e. not a single variable reference.</summary>
    /// <param name="value">The field value.</param>
    /// <returns><see langword="true"/> for a literal secret.</returns>
    internal static bool IsLiteralSecret(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var spans = VariableScanner.ScanSpans(text);
        return !(spans.Count == 1 && spans[0].Start == 0 && spans[0].End == text.Length);
    }
}
