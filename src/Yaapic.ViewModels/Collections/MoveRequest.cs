namespace Yaapic.ViewModels.Collections;

/// <summary>A place a node can be moved to.</summary>
/// <param name="Id">The target folder; null for the top level.</param>
/// <param name="Path">The display path, such as <c>Collection / Folder</c>, or <c>Top level</c>.</param>
public sealed record FolderOption(Guid? Id, string Path);

/// <summary>The argument of the move command.</summary>
/// <param name="Node">The folder or request to move.</param>
/// <param name="Target">Where to move it.</param>
public sealed record MoveRequest(CollectionNodeViewModel Node, FolderOption Target);
