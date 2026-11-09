using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Http;
using Yaapic.Core.Responses;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Responses;

/// <summary>Display-ready state of a response: status, time, size, body, headers, warnings and errors.</summary>
public sealed partial class ResponsePanelViewModel : ObservableObject
{
    private readonly IDialogService? _dialogs;
    private HttpResponseData? _response;

    [ObservableProperty]
    private BodyViewMode _viewMode = BodyViewMode.Pretty;

    private ResponseState _state;
    private string _statusText = "";
    private StatusCategory _statusCategory;
    private string _durationText = "";
    private string _sizeText = "";
    private string _bodyText = "";
    private string _bodyLanguage = "text";
    private bool _isBinary;
    private string _binaryText = "";
    private IReadOnlyList<KeyValueEntry> _headers = [];
    private string? _errorMessage;
    private string? _unresolvedWarning;

    /// <summary>Initializes a new instance of the <see cref="ResponsePanelViewModel"/> class.</summary>
    /// <param name="dialogs">Used to copy and save the body; <see langword="null"/> makes both commands do nothing.</param>
    public ResponsePanelViewModel(IDialogService? dialogs = null) => _dialogs = dialogs;

    /// <summary>Gets the tests and script errors of the last scripted send.</summary>
    public TestResultsViewModel Tests { get; } = new();

    /// <summary>Gets what the panel is showing.</summary>
    public ResponseState State { get => _state; private set => SetProperty(ref _state, value); }

    /// <summary>Gets the status line, such as <c>200 OK</c>; the reason is omitted when empty.</summary>
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    /// <summary>Gets the class of the status code.</summary>
    public StatusCategory StatusCategory { get => _statusCategory; private set => SetProperty(ref _statusCategory, value); }

    /// <summary>Gets the elapsed time, such as <c>120 ms</c> or <c>1.23 s</c>.</summary>
    public string DurationText { get => _durationText; private set => SetProperty(ref _durationText, value); }

    /// <summary>Gets the body size, such as <c>1.5 KB</c>.</summary>
    public string SizeText { get => _sizeText; private set => SetProperty(ref _sizeText, value); }

    /// <summary>Gets the decoded body for the current <see cref="ViewMode"/>; empty for a binary body.</summary>
    public string BodyText { get => _bodyText; private set => SetProperty(ref _bodyText, value); }

    /// <summary>Gets the body language: <c>json</c>, <c>xml</c>, <c>html</c> or <c>text</c>.</summary>
    public string BodyLanguage { get => _bodyLanguage; private set => SetProperty(ref _bodyLanguage, value); }

    /// <summary>Gets a value indicating whether the body is binary.</summary>
    public bool IsBinary { get => _isBinary; private set => SetProperty(ref _isBinary, value); }

    /// <summary>Gets the placeholder shown for a binary body, such as <c>Binary content, 1.5 KB</c>.</summary>
    public string BinaryText { get => _binaryText; private set => SetProperty(ref _binaryText, value); }

    /// <summary>Gets the response headers in wire order.</summary>
    public IReadOnlyList<KeyValueEntry> Headers { get => _headers; private set => SetProperty(ref _headers, value); }

    /// <summary>Gets the failure message, or <see langword="null"/> when the request did not fail.</summary>
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    /// <summary>Gets the unresolved-variables warning, or <see langword="null"/> when every variable resolved.</summary>
    public string? UnresolvedWarning { get => _unresolvedWarning; private set => SetProperty(ref _unresolvedWarning, value); }

    /// <summary>Shows that a request is in flight and drops the previous result.</summary>
    public void ShowSending()
    {
        Clear();
        State = ResponseState.Sending;
    }

    /// <summary>Shows the outcome of a send.</summary>
    /// <param name="report">The report to display.</param>
    public void ShowReport(SendReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Clear();
        UnresolvedWarning = report.UnresolvedVariables.Count == 0
            ? null
            : "Unresolved variables: " + string.Join(", ", report.UnresolvedVariables);

        if (report.Result.Response is not { } response)
        {
            State = ResponseState.Failed;
            ErrorMessage = report.Result.Failure?.Message ?? "The request failed.";
            return;
        }

        _response = response;
        State = ResponseState.Completed;
        StatusText = response.ReasonPhrase.Length == 0
            ? response.StatusCode.ToString(CultureInfo.InvariantCulture)
            : $"{response.StatusCode} {response.ReasonPhrase}";
        StatusCategory = (response.StatusCode / 100) switch
        {
            2 => StatusCategory.Success,
            3 => StatusCategory.Redirect,
            4 => StatusCategory.ClientError,
            5 => StatusCategory.ServerError,
            _ => StatusCategory.Informational,
        };
        DurationText = FormatDuration(response.Duration);
        SizeText = FormatSize(response.SizeBytes);
        Headers = response.Headers;
        ApplyBody();
    }

    /// <summary>Shows the outcome of a send with scripts.</summary>
    /// <param name="report">The scripted report to display.</param>
    public void ShowScripted(ScriptedSendReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.AbortedBy is { } aborted)
        {
            Clear();
            State = ResponseState.Failed;
            var message = aborted.Error?.Message;
            ErrorMessage = $"Pre-request script failed — {aborted.Origin}: "
                + (string.IsNullOrEmpty(message) ? ScriptErrorItemViewModel.StatusTextFor(aborted.Status) : message);
        }
        else
        {
            ShowReport(report.Report!);
        }

        Tests.Show(report.Runs);
    }

    /// <summary>Resets the panel to <see cref="ResponseState.Empty"/>.</summary>
    public void Clear()
    {
        _response = null;
        State = ResponseState.Empty;
        StatusText = "";
        StatusCategory = StatusCategory.Informational;
        DurationText = "";
        SizeText = "";
        BodyText = "";
        BodyLanguage = "text";
        IsBinary = false;
        BinaryText = "";
        Headers = [];
        ErrorMessage = null;
        UnresolvedWarning = null;
        Tests.Clear();
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        CopyBodyCommand.NotifyCanExecuteChanged();
        SaveBodyCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopyBody() => _response is not null && !IsBinary;

    private bool CanSaveBody() => _response is not null;

    [RelayCommand(CanExecute = nameof(CanCopyBody))]
    private async Task CopyBodyAsync()
    {
        if (_dialogs is not null)
        {
            await _dialogs.SetClipboardTextAsync(BodyText);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveBody))]
    private async Task SaveBodyAsync()
    {
        if (_dialogs is not null && _response is { } response)
        {
            await _dialogs.SaveFileAsync(SuggestedName(response.ContentType), response.Body);
        }
    }

    private static string SuggestedName(string? contentType)
    {
        var mediaType = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var extension = mediaType switch
        {
            "application/json" => "json",
            "application/xml" or "text/xml" => "xml",
            "text/html" => "html",
            "text/javascript" or "application/javascript" => "js",
            _ when mediaType.StartsWith("text/", StringComparison.Ordinal) => "txt",
            _ => mediaType.EndsWith("+json", StringComparison.Ordinal) ? "json"
                : mediaType.EndsWith("+xml", StringComparison.Ordinal) ? "xml" : "bin",
        };
        return "response." + extension;
    }

    partial void OnViewModeChanged(BodyViewMode value) => ApplyBody();

    private void ApplyBody()
    {
        if (_response is null)
        {
            return;
        }

        var body = ResponseFormatter.Format(_response, ViewMode);
        BodyText = body.Text;
        BodyLanguage = body.Language;
        IsBinary = body.IsBinary;
        BinaryText = body.IsBinary ? $"Binary content, {SizeText}" : "";
        RefreshCommands();
    }

    internal static string FormatDuration(TimeSpan duration) => ResponseText.FormatDuration(duration);

    private static string FormatSize(long bytes) => ResponseText.FormatSize(bytes);
}
