using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable state of one request tab, convertible to and from <see cref="HttpRequestDefinition"/>.</summary>
public sealed partial class RequestEditorViewModel : ObservableObject
{
    private const string DefaultName = "New Request";

    private readonly RequestSender _sender;
    private readonly IVariableScopeProvider _scopes;
    private readonly IDialogService? _dialogs;
    private HttpRequestDefinition? _baseline;
    private bool _syncingUrl;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowsBody))]
    private HttpMethodKind _method;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _url = "";

    [ObservableProperty]
    private BodyKind _bodyKind;

    [ObservableProperty]
    private RawContentType _bodyContentType;

    [ObservableProperty]
    private string _bodyText = "";

    [ObservableProperty]
    private string _binaryFilePath = "";

    [ObservableProperty]
    private bool _followRedirects = true;

    [ObservableProperty]
    private int _timeoutSeconds;

    [ObservableProperty]
    private bool _isNew;

    /// <summary>Gets or sets a value indicating whether this is the active tab; maintained by the tab list.</summary>
    [ObservableProperty]
    private bool _isActive;

    private RequestEditorViewModel(Guid id, RequestSender sender, IVariableScopeProvider scopes, IDialogService? dialogs)
    {
        Id = id;
        _sender = sender;
        _scopes = scopes;
        _dialogs = dialogs;
        Variables = new VariableContext(id, scopes);
        Response = new ResponsePanelViewModel(dialogs);
        FormData = new FormDataEditorViewModel(dialogs);
        SendCommand.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
            {
                OnPropertyChanged(nameof(IsSending));
            }
        };
        QueryParams.Changed += (_, _) => ComposeUrlFromParams();
        Headers.Changed += (_, _) => Refresh();
        Auth.Changed += (_, _) => Refresh();
        UrlEncodedFields.Changed += (_, _) => Refresh();
        FormData.Changed += (_, _) => Refresh();
    }

    /// <summary>Raised after a send completed, successfully or not; not raised when the user canceled it.</summary>
    public event EventHandler<RequestSentEventArgs>? Sent;

    /// <summary>Gets the HTTP methods offered by the editor.</summary>
    public static IReadOnlyList<HttpMethodKind> Methods { get; } = [
        HttpMethodKind.Get,
        HttpMethodKind.Post,
        HttpMethodKind.Put,
        HttpMethodKind.Patch,
        HttpMethodKind.Delete,
        HttpMethodKind.Head,
        HttpMethodKind.Options,
    ];

    /// <summary>Gets the body kinds offered by the editor.</summary>
    public static IReadOnlyList<BodyKind> BodyKinds { get; } = [BodyKind.None, BodyKind.Raw, BodyKind.UrlEncoded, BodyKind.FormData, BodyKind.Binary];

    /// <summary>Gets the raw content types offered by the editor.</summary>
    public static IReadOnlyList<RawContentType> ContentTypes { get; } = [RawContentType.Json, RawContentType.Text, RawContentType.Xml, RawContentType.Html, RawContentType.JavaScript];

    /// <summary>Gets the request identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the query parameter editor.</summary>
    public KeyValueEditorViewModel QueryParams { get; } = new();

    /// <summary>Gets the header editor.</summary>
    public KeyValueEditorViewModel Headers { get; } = new();

    /// <summary>Gets the url-encoded body field editor.</summary>
    public KeyValueEditorViewModel UrlEncodedFields { get; } = new();

    /// <summary>Gets the multipart body field editor.</summary>
    public FormDataEditorViewModel FormData { get; }

    /// <summary>Gets the authentication editor.</summary>
    public AuthEditorViewModel Auth { get; } = new();

    /// <summary>Gets a value indicating whether the current method carries a body.</summary>
    public bool AllowsBody => Method.AllowsBody();

    /// <summary>Gets the response panel; it keeps the last response of this tab.</summary>
    public ResponsePanelViewModel Response { get; }

    /// <summary>Gets a value indicating whether a send is in flight.</summary>
    public bool IsSending => SendCommand.IsRunning;

    /// <summary>Gets the command that cancels the send in flight.</summary>
    public ICommand CancelCommand => SendCancelCommand;

    /// <summary>Gets a value indicating whether the request is new or differs from its last saved state.</summary>
    public bool IsDirty => IsNew || _baseline is null || !ToDefinition().IsEquivalentTo(_baseline);

    /// <summary>Gets the tab title: the name (or "New Request"), followed by a bullet when dirty.</summary>
    public string Title => DisplayName + (IsDirty ? " •" : "");

    /// <summary>Gets the upper-case method label shown in the tab header.</summary>
    public string MethodText => Method.ToText();

    /// <summary>Gets the variable tokeniser and popup factory of this tab.</summary>
    public VariableContext Variables { get; }

    /// <summary>Gets the tab name without the dirty marker: the trimmed name, or "New Request" when it is empty.</summary>
    public string DisplayName => Name.Trim().Length == 0 ? DefaultName : Name.Trim();

    /// <summary>Creates an editor showing <paramref name="definition"/>, with it as the saved baseline.</summary>
    /// <param name="definition">The request to edit.</param>
    /// <param name="sender">The use case that sends the request.</param>
    /// <param name="scopes">Provides the variable scopes at send time.</param>
    /// <param name="isNew">Whether the request has never been saved.</param>
    /// <param name="dialogs">Used to pick files; <see langword="null"/> makes the pick commands do nothing.</param>
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel FromDefinition(
        HttpRequestDefinition definition,
        RequestSender sender,
        IVariableScopeProvider scopes,
        bool isNew = false,
        IDialogService? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(scopes);
        var editor = new RequestEditorViewModel(definition.Id, sender, scopes, dialogs)
        {
            Name = definition.Name,
            Method = definition.Method,
            BodyKind = definition.Body.Kind,
            BodyContentType = definition.Body.ContentType,
            BodyText = definition.Body.Text,
            BinaryFilePath = definition.Body.FilePath,
            FollowRedirects = definition.Settings.FollowRedirects,
            TimeoutSeconds = definition.Settings.TimeoutSeconds,
        };
        editor.Auth.Load(definition.Auth);
        editor.LoadUrlAndParams(definition);
        editor.Headers.Load(definition.Headers);
        if (definition.Body.Kind == BodyKind.UrlEncoded)
        {
            editor.UrlEncodedFields.Load(definition.Body.Fields.Select(f => new KeyValueEntry(f.Key, f.Value, f.Enabled)));
        }
        else if (definition.Body.Kind == BodyKind.FormData)
        {
            editor.FormData.Load(definition.Body.Fields);
        }

        editor._baseline = editor.ToDefinition();
        editor.IsNew = isNew;
        return editor;
    }

    /// <summary>Creates an editor for a new, unsaved request.</summary>
    /// <param name="sender">The use case that sends the request.</param>
    /// <param name="scopes">Provides the variable scopes at send time.</param>
    /// <param name="dialogs">Used to pick files; <see langword="null"/> makes the pick commands do nothing.</param>
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel CreateNew(RequestSender sender, IVariableScopeProvider scopes, IDialogService? dialogs = null) =>
        FromDefinition(HttpRequestDefinition.Create(DefaultName), sender, scopes, isNew: true, dialogs);

    /// <summary>Builds the domain request from the current state.</summary>
    /// <returns>The request; a blank name becomes "New Request".</returns>
    public HttpRequestDefinition ToDefinition() => new(
        Id,
        Name.Trim().Length == 0 ? DefaultName : Name.Trim(),
        Method,
        QueryStringSync.Compose(UrlPartsOfCurrent() with { Params = [] }),
        QueryParams.ToEntries(),
        Headers.ToEntries(),
        new RequestBody(BodyKind, BodyContentType, BodyText, BodyFields(), BodyKind == BodyKind.Binary ? BinaryFilePath : ""),
        Auth.ToAuth(),
        new RequestSettings(FollowRedirects, TimeoutSeconds));

    /// <summary>Records the current state as saved: the request is no longer new nor dirty.</summary>
    public void MarkSaved()
    {
        _baseline = ToDefinition();
        IsNew = false;
        Refresh();
    }

    [RelayCommand]
    private async Task PickBinaryFileAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var path = await _dialogs.PickFileAsync("Select a file");
        if (path is not null)
        {
            BinaryFilePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken ct)
    {
        Response.ShowSending();
        var request = ToDefinition();
        var report = await _sender.SendAsync(request, _scopes.GetScopes(Id), ct);
        Response.ShowReport(report);
        if (report.Result.Failure?.Kind != SendFailureKind.Canceled)
        {
            Sent?.Invoke(this, new RequestSentEventArgs(request, report));
        }
    }

    private bool CanSend() => Url.Trim().Length > 0;

    partial void OnNameChanged(string value) => Refresh();

    partial void OnMethodChanged(HttpMethodKind value) => Refresh();

    partial void OnUrlChanged(string value)
    {
        if (!_syncingUrl)
        {
            ParseUrlIntoParams(value);
        }

        Refresh();
    }

    partial void OnIsActiveChanged(bool value) => Variables.IsActive = value;

    partial void OnBodyKindChanged(BodyKind value) => Refresh();

    partial void OnBodyContentTypeChanged(RawContentType value) => Refresh();

    partial void OnBodyTextChanged(string value) => Refresh();

    partial void OnBinaryFilePathChanged(string value) => Refresh();

    partial void OnFollowRedirectsChanged(bool value) => Refresh();

    partial void OnTimeoutSecondsChanged(int value)
    {
        if (value < 0)
        {
            TimeoutSeconds = 0;
            return;
        }

        Refresh();
    }

    partial void OnIsNewChanged(bool value) => Refresh();

    private UrlParts UrlPartsOfCurrent() => QueryStringSync.Parse(Url);

    // Legacy stored URLs may hold an inline query: its entries go first in the table.
    private void LoadUrlAndParams(HttpRequestDefinition definition)
    {
        var parts = QueryStringSync.Parse(definition.Url);
        QueryParams.Load([.. parts.Params, .. definition.QueryParams]);
        _syncingUrl = true;
        try
        {
            Url = ComposeFullUrl(parts.Prefix, parts.Fragment);
        }
        finally
        {
            _syncingUrl = false;
        }
    }

    private string ComposeFullUrl(string prefix, string fragment) =>
        QueryStringSync.Compose(prefix, [.. QueryParams.ToEntries().Where(e => e.Enabled)], fragment);

    private void ComposeUrlFromParams()
    {
        var parts = UrlPartsOfCurrent();
        _syncingUrl = true;
        try
        {
            Url = ComposeFullUrl(parts.Prefix, parts.Fragment);
        }
        finally
        {
            _syncingUrl = false;
        }

        Refresh();
    }

    // The i-th enabled row with a key takes the i-th parsed entry; extra rows go, extra entries are appended.
    private void ParseUrlIntoParams(string url)
    {
        var parsed = QueryStringSync.Parse(url).Params;
        List<KeyValueEntry> merged = [];
        var next = 0;
        foreach (var row in QueryParams.ToEntries())
        {
            if (!row.Enabled || row.Key.Length == 0)
            {
                merged.Add(row);
            }
            else if (next < parsed.Count)
            {
                merged.Add(parsed[next++]);
            }
        }

        merged.AddRange(parsed.Skip(next));
        QueryParams.Load(merged);
    }

    private IReadOnlyList<FormField> BodyFields() => BodyKind switch
    {
        BodyKind.UrlEncoded => [.. UrlEncodedFields.ToEntries().Select(e => new FormField(e.Key, e.Value, e.Enabled))],
        BodyKind.FormData => FormData.ToFields(),
        _ => [],
    };

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(MethodText));
    }
}
