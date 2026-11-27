namespace Yaapic.Infrastructure.Tests;

/// <summary>Runs alone: the language is process-wide, so no other test may run while French is selected.</summary>
[CollectionDefinition("Localizer", DisableParallelization = true)]
public sealed class LocalizerCollection;
