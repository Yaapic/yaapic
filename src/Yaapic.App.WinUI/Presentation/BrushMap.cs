using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Maps domain values to the generated <c>Yaapic*</c> brushes; used through <c>x:Bind</c> function binding.</summary>
public static class BrushMap
{
    /// <summary>Gets the brush that colors an HTTP method label.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The method brush from the application resources.</returns>
    public static Brush Method(HttpMethodKind method) =>
        Get(method == HttpMethodKind.Post ? "YaapicMethodPostBrush" : "YaapicMethodGetBrush");

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

    private static Brush Get(string key) => (Brush)Application.Current.Resources[key];
}
