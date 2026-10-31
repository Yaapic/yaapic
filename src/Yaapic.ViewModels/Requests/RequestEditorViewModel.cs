using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable state of one request tab, convertible to and from <see cref="HttpRequestDefinition"/>.</summary>
public sealed partial class RequestEditorViewModel : ObservableObject
{
    private const string DefaultName = "New Request";

    private readonly RequestSender _sender;
    private readonly IActiveEnvironmentProvider _environments;
    private HttpRequestDefinition? _baseline;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
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
    private bool _isNew;

    private RequestEditorViewModel(Guid id, RequestSender sender, IActiveEnvironmentProvider environments)
    {
        Id = id;
        _sender = sender;
        _environments = environments;
        SendCommand.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
            {
                OnPropertyChanged(nameof(IsSending));
            }
        };
        QueryParams.Changed += (_, _) => Refresh();
        Headers.Changed += (_, _) => Refresh();
    }

    /// <summary>Gets the HTTP methods offered by the editor.</summary>
    public static IReadOnlyList<HttpMethodKind> Methods { get; } = [HttpMethodKind.Get, HttpMethodKind.Post];

    /// <summary>Gets the body kinds offered by the editor.</summary>
    public static IReadOnlyList<BodyKind> BodyKinds { get; } = [BodyKind.None, BodyKind.Raw];

    /// <summary>Gets the raw content types offered by the editor.</summary>
    public static IReadOnlyList<RawContentType> ContentTypes { get; } = [RawContentType.Json, RawContentType.Text, RawContentType.Xml];

    /// <summary>Gets the request identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the query parameter editor.</summary>
    public KeyValueEditorViewModel QueryParams { get; } = new();

    /// <summary>Gets the header editor.</summary>
    public KeyValueEditorViewModel Headers { get; } = new();

    /// <summary>Gets the response panel; it keeps the last response of this tab.</summary>
    public ResponsePanelViewModel Response { get; } = new();

    /// <summary>Gets a value indicating whether a send is in flight.</summary>
    public bool IsSending => SendCommand.IsRunning;

    /// <summary>Gets the command that cancels the send in flight.</summary>
    public ICommand CancelCommand => SendCancelCommand;

    /// <summary>Gets a value indicating whether the request is new or differs from its last saved state.</summary>
    public bool IsDirty => IsNew || _baseline is null || !ToDefinition().IsEquivalentTo(_baseline);

    /// <summary>Gets the tab title: the name (or "New Request"), followed by a bullet when dirty.</summary>
    public string Title => DisplayName + (IsDirty ? " •" : "");

    /// <summary>Gets the upper-case method label shown in the tab header.</summary>
    public string MethodText => Method == HttpMethodKind.Post ? "POST" : "GET";

    /// <summary>Gets the tab name without the dirty marker: the trimmed name, or "New Request" when it is empty.</summary>
    public string DisplayName => Name.Trim().Length == 0 ? DefaultName : Name.Trim();

    /// <summary>Creates an editor showing <paramref name="definition"/>, with it as the saved baseline.</summary>
    /// <param name="definition">The request to edit.</param>
    /// <param name="sender">The use case that sends the request.</param>
    /// <param name="environments">Provides the active environment at send time.</param>
    /// <param name="isNew">Whether the request has never been saved.</param>
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel FromDefinition(
        HttpRequestDefinition definition,
        RequestSender sender,
        IActiveEnvironmentProvider environments,
        bool isNew = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(environments);
        var editor = new RequestEditorViewModel(definition.Id, sender, environments)
        {
            Name = definition.Name,
            Method = definition.Method,
            Url = definition.Url,
            BodyKind = definition.Body.Kind,
            BodyContentType = definition.Body.ContentType,
            BodyText = definition.Body.Text,
        };
        editor.QueryParams.Load(definition.QueryParams);
        editor.Headers.Load(definition.Headers);
        editor._baseline = editor.ToDefinition();
        editor.IsNew = isNew;
        return editor;
    }

    /// <summary>Creates an editor for a new, unsaved request.</summary>
    /// <param name="sender">The use case that sends the request.</param>
    /// <param name="environments">Provides the active environment at send time.</param>
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel CreateNew(RequestSender sender, IActiveEnvironmentProvider environments) =>
        FromDefinition(HttpRequestDefinition.Create(DefaultName), sender, environments, isNew: true);

    /// <summary>Builds the domain request from the current state.</summary>
    /// <returns>The request; a blank name becomes "New Request".</returns>
    public HttpRequestDefinition ToDefinition() => new(
        Id,
        Name.Trim().Length == 0 ? DefaultName : Name.Trim(),
        Method,
        Url,
        QueryParams.ToEntries(),
        Headers.ToEntries(),
        new RequestBody(BodyKind, BodyContentType, BodyText));

    /// <summary>Records the current state as saved: the request is no longer new nor dirty.</summary>
    public void MarkSaved()
    {
        _baseline = ToDefinition();
        IsNew = false;
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken ct)
    {
        Response.ShowSending();
        var report = await _sender.SendAsync(ToDefinition(), _environments.Current, ct);
        Response.ShowReport(report);
    }

    private bool CanSend() => Url.Trim().Length > 0;

    partial void OnNameChanged(string value) => Refresh();

    partial void OnMethodChanged(HttpMethodKind value) => Refresh();

    partial void OnUrlChanged(string value) => Refresh();

    partial void OnBodyKindChanged(BodyKind value) => Refresh();

    partial void OnBodyContentTypeChanged(RawContentType value) => Refresh();

    partial void OnBodyTextChanged(string value) => Refresh();

    partial void OnIsNewChanged(bool value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(MethodText));
    }
}
