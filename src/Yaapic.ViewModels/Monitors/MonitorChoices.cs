namespace Yaapic.ViewModels.Monitors;

/// <summary>A folder a monitor can run.</summary>
/// <param name="Id">The folder identifier.</param>
/// <param name="Path">The folder path in the collection tree, separated by <c>/</c>.</param>
public sealed record MonitorFolderChoice(Guid Id, string Path);

/// <summary>An environment a monitor can run with.</summary>
/// <param name="Id">The environment identifier, or <see langword="null"/> for none.</param>
/// <param name="Name">The display name.</param>
public sealed record MonitorEnvironmentChoice(Guid? Id, string Name);

/// <summary>An interval a monitor can run at.</summary>
/// <param name="Minutes">The minutes between two runs.</param>
/// <param name="Text">The display text, such as <c>5 min</c> or <c>24 h</c>.</param>
public sealed record MonitorIntervalChoice(int Minutes, string Text);
