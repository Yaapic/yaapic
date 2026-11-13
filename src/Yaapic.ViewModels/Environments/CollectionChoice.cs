namespace Yaapic.ViewModels.Environments;

/// <summary>One entry of the "available in" list of an environment: a collection, or every collection.</summary>
/// <param name="Id">The collection identifier; <see langword="null"/> for "all collections".</param>
/// <param name="Name">The text shown to the user.</param>
public sealed record CollectionChoice(Guid? Id, string Name);
