using CommunityToolkit.Mvvm.ComponentModel;

namespace Yaapic.ViewModels.Import;

/// <summary>One collection (top-level folder) that can be exported.</summary>
/// <param name="Id">The folder identifier.</param>
/// <param name="Name">The folder name.</param>
public sealed record ExportChoice(Guid Id, string Name);

/// <summary>One export format.</summary>
/// <param name="Id">The exporter identifier.</param>
/// <param name="DisplayName">The name shown to the user.</param>
public sealed record ExportFormatChoice(string Id, string DisplayName);

/// <summary>One environment of the list, with its selection state.</summary>
/// <param name="id">The environment identifier.</param>
/// <param name="name">The environment name.</param>
/// <param name="collectionId">The collection the environment is scoped to, or <see langword="null"/> when global.</param>
/// <param name="collectionName">The name of that collection, or <see langword="null"/> when global or unknown.</param>
public sealed partial class ExportEnvironmentChoice(Guid id, string name, Guid? collectionId, string? collectionName) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Gets the environment identifier.</summary>
    public Guid Id { get; } = id;

    /// <summary>Gets the environment name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the collection the environment is scoped to, or <see langword="null"/> when global.</summary>
    public Guid? CollectionId { get; } = collectionId;

    /// <summary>Gets the name of the collection the environment is scoped to, or <see langword="null"/> when global or unknown.</summary>
    public string? CollectionName { get; } = collectionName;
}
