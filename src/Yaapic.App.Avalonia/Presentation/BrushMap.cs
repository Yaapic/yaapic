using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Maps domain values to the generated <c>Yaapic*</c> brushes; exposed as converters for compiled bindings.</summary>
public static class BrushMap
{
    /// <summary>Converts an HTTP method to the brush that colors its label.</summary>
    public static FuncValueConverter<HttpMethodKind, IBrush?> MethodBrush { get; } = new(m => Method(m));

    /// <summary>Gets the brush that colors an HTTP method label.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The method brush from the application resources, or <see langword="null"/> when missing.</returns>
    public static IBrush? Method(HttpMethodKind method) =>
        Get(method switch
        {
            HttpMethodKind.Post => "YaapicMethodPostBrush",
            HttpMethodKind.Put => "YaapicMethodPutBrush",
            HttpMethodKind.Patch => "YaapicMethodPatchBrush",
            HttpMethodKind.Delete => "YaapicMethodDeleteBrush",
            HttpMethodKind.Head => "YaapicMethodHeadBrush",
            HttpMethodKind.Options => "YaapicMethodOptionsBrush",
            _ => "YaapicMethodGetBrush",
        });

    /// <summary>Gets the text and highlight brushes of a variable token, from the theme variant the control shows.</summary>
    /// <param name="owner">The control whose theme variant and resources are used.</param>
    /// <param name="state">How the variable resolves.</param>
    /// <returns>The foreground and the translucent background; either is <see langword="null"/> when the key is missing.</returns>
    public static (IBrush? Foreground, IBrush? Background) Token(Control owner, VariableTokenState state)
    {
        var (text, fill) = state switch
        {
            VariableTokenState.Resolved => ("YaapicSuccessBrush", "YaapicSuccessBackgroundBrush"),
            VariableTokenState.Dynamic => ("YaapicInfoBrush", "YaapicInfoBackgroundBrush"),
            _ => ("YaapicWarningBrush", "YaapicWarningBackgroundBrush"),
        };
        return (Find(owner, text), Find(owner, fill));
    }

    /// <summary>Looks a brush up in the resources of a control for its current theme variant.</summary>
    /// <param name="owner">The control.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The brush, or <see langword="null"/> when missing.</returns>
    public static IBrush? Find(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var brush) ? brush as IBrush : null;

    internal static IBrush? Get(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var brush) ? brush as IBrush : null;
}

