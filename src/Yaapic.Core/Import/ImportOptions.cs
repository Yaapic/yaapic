namespace Yaapic.Core.Import;

/// <summary>Options an importer may read.</summary>
/// <param name="IncludeStaticResources">Whether HAR entries for images, fonts, styles and scripts are kept.</param>
public sealed record ImportOptions(bool IncludeStaticResources = false)
{
    /// <summary>Gets the default options.</summary>
    public static ImportOptions Default { get; } = new();
}
