using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Import;

/// <summary>A node of an imported collection tree.</summary>
public abstract record ImportItem;

/// <summary>An imported folder with its children.</summary>
/// <param name="Folder">The folder definition.</param>
/// <param name="Children">The sub-folders and requests, in order.</param>
public sealed record ImportFolder(FolderDefinition Folder, IReadOnlyList<ImportItem> Children) : ImportItem;

/// <summary>An imported request.</summary>
/// <param name="Request">The request definition.</param>
public sealed record ImportRequest(HttpRequestDefinition Request) : ImportItem;
