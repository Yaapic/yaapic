using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Export;

/// <summary>What an exporter reads: a collection and/or environments and globals, with secret values blanked.</summary>
/// <param name="Collection">The top-level folder to export, or <see langword="null"/> when only environments are exported.</param>
/// <param name="Environments">The selected environments.</param>
/// <param name="Globals">The global variables, empty when not selected.</param>
/// <param name="Name">The collection name, or <c>Environments</c> when there is no collection.</param>
public sealed record ExportSource(CollectionNode? Collection, IReadOnlyList<EnvironmentDefinition> Environments,
    IReadOnlyList<KeyValueEntry> Globals, string Name);
