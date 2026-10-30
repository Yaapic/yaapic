using Microsoft.UI.Xaml;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Visibility helpers used through <c>x:Bind</c> function binding.</summary>
public static class VisibilityMap
{
    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNull(object? value) => value is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Returns <see cref="Visibility.Visible"/> when the value is not null.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNotNull(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Returns true when the value is not null.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>Whether the value is set.</returns>
    public static bool IsNotNull(object? value) => value is not null;

    /// <summary>Gets whether <paramref name="text"/> holds a message.</summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> when the text is not empty.</returns>
    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="true"/>.</summary>
    /// <param name="value">The flag to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="false"/>.</summary>
    /// <param name="value">The flag to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the method is GET, which is sent without a body.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenGet(HttpMethodKind method) => method == HttpMethodKind.Get ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the body is raw text.</summary>
    /// <param name="kind">The body kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenRaw(BodyKind kind) => kind == BodyKind.Raw ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the panel shows a response.</summary>
    /// <param name="state">The panel state.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenCompleted(ResponseState state) => state == ResponseState.Completed ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the panel has nothing to show.</summary>
    /// <param name="state">The panel state.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenEmpty(ResponseState state) => state == ResponseState.Empty ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> while a request is in flight.</summary>
    /// <param name="state">The panel state.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenSending(ResponseState state) => state == ResponseState.Sending ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the request failed.</summary>
    /// <param name="state">The panel state.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenFailed(ResponseState state) => state == ResponseState.Failed ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the warning is set and the panel is not empty.</summary>
    /// <param name="warning">The unresolved-variables warning.</param>
    /// <param name="state">The panel state.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenWarning(string? warning, ResponseState state) =>
        warning is not null && state != ResponseState.Empty ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the list is empty and no filter is typed.</summary>
    /// <param name="count">The number of listed requests.</param>
    /// <param name="filter">The filter text.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNoSavedRequests(int count, string? filter) =>
        count == 0 && string.IsNullOrWhiteSpace(filter) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when a filter is typed and nothing matches.</summary>
    /// <param name="count">The number of listed requests.</param>
    /// <param name="filter">The filter text.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNoMatch(int count, string? filter) =>
        count == 0 && !string.IsNullOrWhiteSpace(filter) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the message shown when a filter matches no request.</summary>
    /// <param name="filter">The filter text.</param>
    /// <returns>The message.</returns>
    public static string NoMatchText(string? filter) => $"No request matches '{filter?.Trim()}'.";
}
