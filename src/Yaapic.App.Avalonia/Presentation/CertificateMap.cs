using Avalonia.Data.Converters;
using Yaapic.Core.Secrets;
using Yaapic.Localization;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters of the client certificates window.</summary>
public static class CertificateMap
{
    /// <summary>Converts the vault state to <see langword="true"/> when the vault cannot give out passphrases.</summary>
    public static FuncValueConverter<VaultState, bool> IsNotUnlocked { get; } = new(state => state != VaultState.Unlocked);

    /// <summary>Converts the "a passphrase is stored" flag to the placeholder of the passphrase field.</summary>
    public static FuncValueConverter<bool, string?> PassphrasePlaceholder { get; } =
        new(hasPassphrase => hasPassphrase ? Strings.CertificatesKeepCurrent : "");
}
