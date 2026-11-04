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
    public static Brush Method(HttpMethodKind method) => Get(method switch
    {
        HttpMethodKind.Get => "YaapicMethodGetBrush",
        HttpMethodKind.Post => "YaapicMethodPostBrush",
        HttpMethodKind.Put => "YaapicMethodPutBrush",
        HttpMethodKind.Patch => "YaapicMethodPatchBrush",
        HttpMethodKind.Delete => "YaapicMethodDeleteBrush",
        HttpMethodKind.Head => "YaapicMethodHeadBrush",
        HttpMethodKind.Options => "YaapicMethodOptionsBrush",
        _ => "YaapicMethodCustomBrush",
    });

    /// <summary>Gets the method brush of a tree row.</summary>
    /// <param name="method">The HTTP method; null for a folder.</param>
    /// <returns>The method brush, or the custom-method brush for a folder (never shown).</returns>
    public static Brush MethodOrDefault(HttpMethodKind? method) => Method(method ?? HttpMethodKind.Get);

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

    private static Brush Get(string key) => (Brush)Application.Current.Resources[key];
}
