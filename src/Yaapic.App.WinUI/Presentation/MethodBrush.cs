using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Domain.Http;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Colors a <see cref="TextBlock"/> with the brush of an HTTP method and keeps it in step with the element theme.</summary>
public static class MethodBrush
{
    /// <summary>Identifies the attached <c>Method</c> property: an <see cref="HttpMethodKind"/>, or null for the default.</summary>
    public static readonly DependencyProperty MethodProperty =
        DependencyProperty.RegisterAttached("Method", typeof(object), typeof(MethodBrush), new PropertyMetadata(null, OnMethodChanged));

    /// <summary>Gets the method of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The method, boxed, or null.</returns>
    public static object? GetMethod(DependencyObject element) => element.GetValue(MethodProperty);

    /// <summary>Sets the method of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The method, boxed, or null.</param>
    public static void SetMethod(DependencyObject element, object? value) => element.SetValue(MethodProperty, value);

    private static readonly List<WeakReference<TextBlock>> Labels = [];

    private static void OnMethodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        if (!Labels.Exists(w => w.TryGetTarget(out var t) && ReferenceEquals(t, text)))
        {
            Labels.Add(new WeakReference<TextBlock>(text));
        }

        Apply(text);
    }

    /// <summary>Resolves the brush of every live label again, after the window theme changed.</summary>
    public static void RefreshAll()
    {
        Labels.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in Labels)
        {
            if (w.TryGetTarget(out var text))
            {
                Apply(text);
            }
        }
    }

    // A code lookup cannot see the element theme, so the window theme (BrushMap.IsLight) is used and RefreshAll follows changes.
    private static void Apply(TextBlock text) =>
        text.Foreground = BrushMap.Method(GetMethod(text) is HttpMethodKind kind ? kind : HttpMethodKind.Get);
}
