using Avalonia.Data.Converters;
using Yaapic.Domain.Http;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that map request editor values to control state, without reflection.</summary>
public static class EditorMap
{
    /// <summary>Converts an HTTP method to its index in the method picker (the order of <c>RequestEditorViewModel.Methods</c>).</summary>
    public static FuncValueConverter<HttpMethodKind, int> MethodIndex { get; } = new(m => (int)m);

    /// <summary>Converts a raw content type to its index in the picker (JSON, Text, XML, HTML, JavaScript).</summary>
    public static FuncValueConverter<RawContentType, int> ContentTypeIndex { get; } = new(c => c switch
    {
        RawContentType.Json => 0,
        RawContentType.Text => 1,
        RawContentType.Xml => 2,
        RawContentType.Html => 3,
        _ => 4,
    });

    /// <summary>Converts a body kind to <see langword="true"/> when no body is sent.</summary>
    public static FuncValueConverter<BodyKind, bool> IsNone { get; } = new(k => k == BodyKind.None);

    /// <summary>Converts a body kind to <see langword="true"/> when a body is sent.</summary>
    public static FuncValueConverter<BodyKind, bool> IsNotNone { get; } = new(k => k != BodyKind.None);

    /// <summary>Converts a body kind to its index in the body mode bar (none, raw, form-urlencoded, form-data, binary).</summary>
    public static FuncValueConverter<BodyKind, int> BodyKindIndex { get; } = new(k => k switch
    {
        BodyKind.None => 0,
        BodyKind.Raw => 1,
        BodyKind.UrlEncoded => 2,
        BodyKind.FormData => 3,
        _ => 4,
    });

    /// <summary>Converts a count to <see langword="true"/> when it is above zero (tab badges).</summary>
    public static FuncValueConverter<int, bool> IsPositive { get; } = new(n => n > 0);

    /// <summary>Converts a body kind to <see langword="true"/> when the body is raw.</summary>
    public static FuncValueConverter<BodyKind, bool> IsRaw { get; } = new(k => k == BodyKind.Raw);

    /// <summary>Converts a body kind to <see langword="true"/> when the body is url-encoded.</summary>
    public static FuncValueConverter<BodyKind, bool> IsUrlEncoded { get; } = new(k => k == BodyKind.UrlEncoded);

    /// <summary>Converts a body kind to <see langword="true"/> when the body is multipart form-data.</summary>
    public static FuncValueConverter<BodyKind, bool> IsFormData { get; } = new(k => k == BodyKind.FormData);

    /// <summary>Converts a body kind to <see langword="true"/> when the body is a file.</summary>
    public static FuncValueConverter<BodyKind, bool> IsBinary { get; } = new(k => k == BodyKind.Binary);

    /// <summary>Converts a form field kind to its index in the Text/File picker.</summary>
    public static FuncValueConverter<FormFieldKind, int> FieldKindIndex { get; } = new(k => k == FormFieldKind.File ? 1 : 0);

    /// <summary>Converts an authentication kind to its index in the type picker (the order of <c>AuthEditorViewModel.Kinds</c>).</summary>
    public static FuncValueConverter<AuthKind, int> AuthKindIndex { get; } = new(k => k switch
    {
        AuthKind.Basic => 1,
        AuthKind.Bearer => 2,
        AuthKind.ApiKey => 3,
        _ => 0,
    });

    /// <summary>Converts an authentication kind to <see langword="true"/> when no authentication is used.</summary>
    public static FuncValueConverter<AuthKind, bool> IsNoAuth { get; } = new(k => k == AuthKind.None);

    /// <summary>Converts an authentication kind to <see langword="true"/> for Basic authentication.</summary>
    public static FuncValueConverter<AuthKind, bool> IsBasicAuth { get; } = new(k => k == AuthKind.Basic);

    /// <summary>Converts an authentication kind to <see langword="true"/> for Bearer authentication.</summary>
    public static FuncValueConverter<AuthKind, bool> IsBearerAuth { get; } = new(k => k == AuthKind.Bearer);

    /// <summary>Converts an authentication kind to <see langword="true"/> for API key authentication.</summary>
    public static FuncValueConverter<AuthKind, bool> IsApiKeyAuth { get; } = new(k => k == AuthKind.ApiKey);

    /// <summary>Converts an API key location to its index in the "Add to" picker (Header, Query Params).</summary>
    public static FuncValueConverter<ApiKeyLocation, int> LocationIndex { get; } = new(l => l == ApiKeyLocation.Query ? 1 : 0);

    /// <summary>Converts a timeout in seconds to the value of a <c>NumericUpDown</c>.</summary>
    public static FuncValueConverter<int, decimal?> TimeoutValue { get; } = new(n => n);
}
