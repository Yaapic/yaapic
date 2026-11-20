namespace Yaapic.ViewModels.Import;

/// <summary>One entry of the import format list.</summary>
/// <param name="Id">The importer identifier, or <see langword="null"/> for automatic detection.</param>
/// <param name="Name">The name shown to the user.</param>
public sealed record ImportFormatChoice(string? Id, string Name);
