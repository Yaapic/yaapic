using Yaapic.Core.Import;

namespace Yaapic.Core.Export;

/// <summary>The outcome of an export.</summary>
/// <param name="Files">The produced files.</param>
/// <param name="Report">What was skipped or approximated.</param>
public sealed record ExportResult(IReadOnlyList<ExportFile> Files, ImportReport Report);
