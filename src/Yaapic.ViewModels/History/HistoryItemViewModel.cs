using System.Globalization;
using Yaapic.Domain.History;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;
using Yaapic.Localization;

namespace Yaapic.ViewModels.History;

/// <summary>One history entry as shown in the sidebar list.</summary>
public sealed class HistoryItemViewModel
{
    /// <summary>Initializes a new instance of the <see cref="HistoryItemViewModel"/> class.</summary>
    /// <param name="entry">The entry to show.</param>
    /// <param name="localTime">The send time in the local zone.</param>
    public HistoryItemViewModel(HistoryEntry entry, DateTimeOffset localTime)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        Method = entry.Request.Method;
        MethodText = entry.Request.Method.ToText();
        Url = entry.Request.Url;
        Name = entry.Request.Name;
        TimeText = localTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (entry.StatusCode is { } code)
        {
            StatusText = entry.StatusText.Length == 0
                ? code.ToString(CultureInfo.InvariantCulture)
                : $"{code} {entry.StatusText}";
            StatusCategory = (code / 100) switch
            {
                2 => StatusCategory.Success,
                3 => StatusCategory.Redirect,
                4 => StatusCategory.ClientError,
                5 => StatusCategory.ServerError,
                _ => StatusCategory.Informational,
            };
            DurationText = ResponsePanelViewModel.FormatDuration(entry.Duration);
        }
        else
        {
            IsFailed = true;
            StatusText = entry.FailureMessage ?? Strings.RequestFailed;
        }
    }

    /// <summary>Gets the underlying entry.</summary>
    public HistoryEntry Entry { get; }

    /// <summary>Gets the entry identifier.</summary>
    public Guid Id => Entry.Id;

    /// <summary>Gets the HTTP method, which colors the label.</summary>
    public HttpMethodKind Method { get; }

    /// <summary>Gets the upper-case method name.</summary>
    public string MethodText { get; }

    /// <summary>Gets the request URL as stored.</summary>
    public string Url { get; }

    /// <summary>Gets the request name.</summary>
    public string Name { get; }

    /// <summary>Gets "200 OK" style text, or the failure message.</summary>
    public string StatusText { get; }

    /// <summary>Gets a value indicating whether the send produced no response.</summary>
    public bool IsFailed { get; }

    /// <summary>Gets the status class; meaningless when <see cref="IsFailed"/> is set.</summary>
    public StatusCategory StatusCategory { get; }

    /// <summary>Gets the local send time as <c>HH:mm</c>.</summary>
    public string TimeText { get; }

    /// <summary>Gets the response time text, empty when the send failed.</summary>
    public string DurationText { get; } = "";
}
