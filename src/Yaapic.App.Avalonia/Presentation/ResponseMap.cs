using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Core.Responses;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that map response panel values to control state, without reflection.</summary>
public static class ResponseMap
{
    /// <summary>Converts a response state to <see langword="true"/> when the panel is empty.</summary>
    public static FuncValueConverter<ResponseState, bool> IsEmpty { get; } = new(s => s == ResponseState.Empty);

    /// <summary>Converts a response state to <see langword="true"/> while a request is in flight.</summary>
    public static FuncValueConverter<ResponseState, bool> IsSending { get; } = new(s => s == ResponseState.Sending);

    /// <summary>Converts a response state to <see langword="true"/> when the request produced no response.</summary>
    public static FuncValueConverter<ResponseState, bool> IsFailed { get; } = new(s => s == ResponseState.Failed);

    /// <summary>Converts a response state to <see langword="true"/> when a response is shown.</summary>
    public static FuncValueConverter<ResponseState, bool> IsCompleted { get; } = new(s => s == ResponseState.Completed);

    /// <summary>Converts a body view mode to <see langword="true"/> for the Pretty option.</summary>
    public static FuncValueConverter<BodyViewMode, bool> IsPretty { get; } = new(m => m == BodyViewMode.Pretty);

    /// <summary>Converts a body view mode to <see langword="true"/> for the Raw option.</summary>
    public static FuncValueConverter<BodyViewMode, bool> IsRaw { get; } = new(m => m == BodyViewMode.Raw);

    /// <summary>Converts a status category to the text brush of the status chip.</summary>
    public static FuncValueConverter<StatusCategory, IBrush?> StatusForeground { get; } = new(c => BrushMap.Get(c switch
    {
        StatusCategory.Success => "YaapicSuccessBrush",
        StatusCategory.Redirect => "YaapicInfoBrush",
        StatusCategory.ClientError => "YaapicWarningBrush",
        StatusCategory.ServerError => "YaapicDangerBrush",
        _ => "YaapicTextSecondaryBrush",
    }));

    /// <summary>Converts a status category to the background brush of the status chip; informational statuses have none.</summary>
    public static FuncValueConverter<StatusCategory, IBrush?> StatusBackground { get; } = new(c => c switch
    {
        StatusCategory.Success => BrushMap.Get("YaapicSuccessBackgroundBrush"),
        StatusCategory.Redirect => BrushMap.Get("YaapicInfoBackgroundBrush"),
        StatusCategory.ClientError => BrushMap.Get("YaapicWarningBackgroundBrush"),
        StatusCategory.ServerError => BrushMap.Get("YaapicDangerBackgroundBrush"),
        _ => Brushes.Transparent,
    });
}
