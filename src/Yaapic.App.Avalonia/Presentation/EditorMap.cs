using Avalonia.Data.Converters;
using Yaapic.Domain.Http;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that map request editor values to control state, without reflection.</summary>
public static class EditorMap
{
    /// <summary>Converts an HTTP method to its index in the method picker (the order of <c>RequestEditorViewModel.Methods</c>).</summary>
    public static FuncValueConverter<HttpMethodKind, int> MethodIndex { get; } = new(m => (int)m);

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
}
