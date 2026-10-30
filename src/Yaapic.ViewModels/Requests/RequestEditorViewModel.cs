using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable state of one request tab, convertible to and from <see cref="HttpRequestDefinition"/>.</summary>
public sealed partial class RequestEditorViewModel : ObservableObject
{
    private const string DefaultName = "New Request";

    private HttpRequestDefinition? _baseline;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private HttpMethodKind _method;

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private BodyKind _bodyKind;

    [ObservableProperty]
    private RawContentType _bodyContentType;

    [ObservableProperty]
    private string _bodyText = "";

    [ObservableProperty]
    private bool _isNew;

    private RequestEditorViewModel(Guid id)
    {
        Id = id;
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

    /// <summary>Gets a value indicating whether the request is new or differs from its last saved state.</summary>
    public bool IsDirty => IsNew || _baseline is null || !ToDefinition().IsEquivalentTo(_baseline);

    /// <summary>Gets the tab title: the name (or "New Request"), followed by a bullet when dirty.</summary>
    public string Title => (Name.Trim().Length == 0 ? DefaultName : Name.Trim()) + (IsDirty ? " •" : "");

    /// <summary>Creates an editor showing <paramref name="definition"/>, with it as the saved baseline.</summary>
    /// <param name="definition">The request to edit.</param>
    /// <param name="isNew">Whether the request has never been saved.</param>
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel FromDefinition(HttpRequestDefinition definition, bool isNew = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var editor = new RequestEditorViewModel(definition.Id)
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
    /// <returns>The editor.</returns>
    public static RequestEditorViewModel CreateNew() =>
        FromDefinition(HttpRequestDefinition.Create(DefaultName), isNew: true);

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
    }
}
