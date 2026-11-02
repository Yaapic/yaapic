using Avalonia.Data.Converters;
using Yaapic.Domain.Http;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that map request editor values to control state, without reflection.</summary>
public static class EditorMap
{
    /// <summary>Converts an HTTP method to its index in the method picker (GET, POST).</summary>
    public static FuncValueConverter<HttpMethodKind, int> MethodIndex { get; } = new(m => m == HttpMethodKind.Post ? 1 : 0);

    /// <summary>Converts a raw content type to its index in the picker (JSON, Text, XML).</summary>
    public static FuncValueConverter<RawContentType, int> ContentTypeIndex { get; } = new(c => c switch
    {
        RawContentType.Json => 0,
        RawContentType.Text => 1,
        _ => 2,
    });

    /// <summary>Converts a body kind to <see langword="true"/> when no body is sent.</summary>
    public static FuncValueConverter<BodyKind, bool> IsNone { get; } = new(k => k == BodyKind.None);

    /// <summary>Converts a body kind to <see langword="true"/> when the body is raw.</summary>
    public static FuncValueConverter<BodyKind, bool> IsRaw { get; } = new(k => k == BodyKind.Raw);

    /// <summary>Converts an HTTP method to <see langword="true"/> when it is GET.</summary>
    public static FuncValueConverter<HttpMethodKind, bool> IsGet { get; } = new(m => m == HttpMethodKind.Get);
}
