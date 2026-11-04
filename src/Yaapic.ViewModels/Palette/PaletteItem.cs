namespace Yaapic.ViewModels.Palette;

/// <summary>One entry of the command palette.</summary>
/// <param name="Id">Unique, stable id, e.g. <c>command:new-request</c> or <c>request:&lt;guid&gt;</c>.</param>
/// <param name="Title">The text shown and matched against the query.</param>
/// <param name="Subtitle">Optional secondary text.</param>
/// <param name="Category">The group label, e.g. "Command" or "Request".</param>
/// <param name="Shortcut">Optional keyboard shortcut hint.</param>
/// <param name="Execute">The action run when the item is chosen.</param>
public sealed record PaletteItem(string Id, string Title, string? Subtitle, string Category, string? Shortcut, Func<Task> Execute);
