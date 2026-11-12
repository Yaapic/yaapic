using Yaapic.Localization;
using Avalonia.Data.Converters;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters of the vault status bar item.</summary>
public static class VaultMap
{
    /// <summary>Converts the protector label (<see langword="null"/> when no vault exists) to the tooltip of the vault item.</summary>
    public static FuncValueConverter<string?, string> Tip { get; } = new(label => label ?? Strings.VaultUnprotected);
}
