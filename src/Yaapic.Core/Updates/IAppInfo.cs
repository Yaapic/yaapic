namespace Yaapic.Core.Updates;

/// <summary>The release channel a version belongs to.</summary>
public enum AppChannel
{
    /// <summary>Versions without a pre-release suffix.</summary>
    Stable,

    /// <summary>Versions with a pre-release suffix (previews, development builds).</summary>
    Preview,
}

/// <summary>Describes the running application.</summary>
public interface IAppInfo
{
    /// <summary>Gets the running version.</summary>
    AppVersion Version { get; }

    /// <summary>Gets the channel of the running version.</summary>
    AppChannel Channel { get; }
}

/// <summary>The default <see cref="IAppInfo"/>: the channel follows the version suffix.</summary>
/// <param name="Version">The running version.</param>
public sealed record AppInfo(AppVersion Version) : IAppInfo
{
    /// <inheritdoc />
    public AppChannel Channel => Version.IsPrerelease ? AppChannel.Preview : AppChannel.Stable;
}
