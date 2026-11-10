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

    /// <summary>Converts a body view mode to the index of its option in the Pretty / Raw control.</summary>
    public static FuncValueConverter<BodyViewMode, int> ViewModeIndex { get; } = new(m => m == BodyViewMode.Raw ? 1 : 0);

    /// <summary>Converts a test outcome (<see langword="true"/> when passed) to the success or danger brush.</summary>
    public static FuncValueConverter<bool, IBrush?> TestOutcome { get; } = new(passed => BrushMap.Get(passed ? "YaapicSuccessBrush" : "YaapicDangerBrush"));

    /// <summary>Converts a test filter to the index of its option in the All / Failed control.</summary>
    public static FuncValueConverter<TestFilter, int> FilterIndex { get; } = new(f => f == TestFilter.Failed ? 1 : 0);

    /// <summary>Converts two flags to <see langword="true"/> when neither is set.</summary>
    public static FuncMultiValueConverter<bool, bool> NeitherFlag { get; } = new(values => !values.Any(v => v));

    /// <summary>Converts a status category to <see langword="true"/> for the success icon (2xx).</summary>
    public static FuncValueConverter<StatusCategory, bool> IsSuccess { get; } = new(c => c == StatusCategory.Success);

    /// <summary>Converts a status category to <see langword="true"/> for the client error icon (4xx).</summary>
    public static FuncValueConverter<StatusCategory, bool> IsClientError { get; } = new(c => c == StatusCategory.ClientError);

    /// <summary>Converts a status category to <see langword="true"/> for the server error icon (5xx).</summary>
    public static FuncValueConverter<StatusCategory, bool> IsServerError { get; } = new(c => c == StatusCategory.ServerError);

    /// <summary>Converts a status category to <see langword="true"/> for the info icon (1xx, 3xx and unknown codes).</summary>
    public static FuncValueConverter<StatusCategory, bool> IsInfo { get; } = new(c => c is StatusCategory.Informational or StatusCategory.Redirect);
}
