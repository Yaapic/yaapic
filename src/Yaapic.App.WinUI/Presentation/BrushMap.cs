using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Maps domain values to the generated <c>Yaapic*</c> brushes; used through <c>x:Bind</c> function binding.</summary>
public static class BrushMap
{
    /// <summary>Gets the brush that colors an HTTP method label.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The method brush from the application resources.</returns>
    public static Brush Method(HttpMethodKind method) => Method(method, IsLight);

    /// <summary>Gets the brush that colors an HTTP method label in a given theme.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="light">Whether the element shows the light theme.</param>
    /// <returns>The method brush of that theme.</returns>
    public static Brush Method(HttpMethodKind method, bool light) => Get(method switch
    {
        HttpMethodKind.Get => "YaapicMethodGetBrush",
        HttpMethodKind.Post => "YaapicMethodPostBrush",
        HttpMethodKind.Put => "YaapicMethodPutBrush",
        HttpMethodKind.Patch => "YaapicMethodPatchBrush",
        HttpMethodKind.Delete => "YaapicMethodDeleteBrush",
        HttpMethodKind.Head => "YaapicMethodHeadBrush",
        HttpMethodKind.Options => "YaapicMethodOptionsBrush",
        _ => "YaapicMethodCustomBrush",
    }, light);

    /// <summary>Gets the text brush of the status chip.</summary>
    /// <param name="category">The class of the status code.</param>
    /// <returns>The brush from the application resources.</returns>
    public static Brush StatusForeground(StatusCategory category) => Get(category switch
    {
        StatusCategory.Success => "YaapicSuccessBrush",
        StatusCategory.Redirect => "YaapicInfoBrush",
        StatusCategory.ClientError => "YaapicWarningBrush",
        StatusCategory.ServerError => "YaapicDangerBrush",
        _ => "YaapicTextSecondaryBrush",
    });

    /// <summary>Gets the background brush of the status chip; informational statuses have none.</summary>
    /// <param name="category">The class of the status code.</param>
    /// <returns>The brush from the application resources, or a transparent brush.</returns>
    public static Brush StatusBackground(StatusCategory category) => category switch
    {
        StatusCategory.Success => Get("YaapicSuccessBackgroundBrush"),
        StatusCategory.Redirect => Get("YaapicInfoBackgroundBrush"),
        StatusCategory.ClientError => Get("YaapicWarningBackgroundBrush"),
        StatusCategory.ServerError => Get("YaapicDangerBackgroundBrush"),
        _ => new SolidColorBrush(Microsoft.UI.Colors.Transparent),
    };

    /// <summary>Gets the text brush of a history row status.</summary>
    /// <param name="category">The class of the status code.</param>
    /// <param name="isFailed">Whether the send produced no response.</param>
    /// <returns>The danger brush for a failed send, else the status brush.</returns>
    public static Brush HistoryStatus(StatusCategory category, bool isFailed) =>
        isFailed ? Get("YaapicDangerBrush") : StatusForeground(category);

    /// <summary>Gets the icon brush of the collections rail entry.</summary>
    /// <param name="mode">The sidebar mode.</param>
    /// <returns>The accent brush when selected, else the secondary text brush.</returns>
    public static Brush CollectionsRail(SidebarMode mode) => Rail(mode == SidebarMode.Collections);

    /// <summary>Gets the icon brush of the history rail entry.</summary>
    /// <param name="mode">The sidebar mode.</param>
    /// <returns>The accent brush when selected, else the secondary text brush.</returns>
    public static Brush HistoryRail(SidebarMode mode) => Rail(mode == SidebarMode.History);

    private static Brush Rail(bool selected) => Get(selected ? "YaapicAccentTextBrush" : "YaapicTextSecondaryBrush");

    /// <summary>Gets or sets whether the window shows the light theme; set by the main window, which owns the element theme.</summary>
    public static bool IsLight { get; set; }

    // A code lookup in Application.Resources only sees the application theme (dark), so the theme dictionary of the
    // window theme is searched first. Elements already bound keep their brush until their binding is evaluated again.
    /// <summary>Gets a brush by resource key from the dictionary of the window theme.</summary>
    /// <param name="key">The resource key, such as <c>YaapicSuccessBrush</c>.</param>
    /// <returns>The brush of the current window theme.</returns>
    public static Brush Themed(string key) => Get(key, IsLight);

    private static Brush Get(string key) => Get(key, IsLight);

    private static Brush Get(string key, bool light) =>
        FindThemed(Application.Current.Resources, light ? "Light" : "Dark", key) ?? (Brush)Application.Current.Resources[key];

    private static Brush? FindThemed(ResourceDictionary dictionary, string theme, string key)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(theme, out var themed)
            && themed is ResourceDictionary colors
            && colors.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (FindThemed(merged, theme, key) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
