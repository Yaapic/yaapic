using System.Globalization;

namespace Yaapic.Core.Updates;

/// <summary>A semantic version (MAJOR.MINOR.PATCH with an optional pre-release suffix), ordered following SemVer 2.0.</summary>
public sealed record AppVersion : IComparable<AppVersion>
{
    private AppVersion(int major, int minor, int patch, string prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    /// <summary>Gets the major number.</summary>
    public int Major { get; }

    /// <summary>Gets the minor number.</summary>
    public int Minor { get; }

    /// <summary>Gets the patch number.</summary>
    public int Patch { get; }

    /// <summary>Gets the pre-release suffix without the leading dash, empty for a stable version.</summary>
    public string Prerelease { get; }

    /// <summary>Gets a value indicating whether the version carries a pre-release suffix.</summary>
    public bool IsPrerelease => Prerelease.Length > 0;

    /// <summary>Parses a version such as <c>1.2.3</c>, <c>1.2.3-preview.4</c> or <c>1.2.3+abc</c> (build metadata is dropped).</summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The version.</returns>
    /// <exception cref="FormatException">The text is not a valid version.</exception>
    public static AppVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a valid version.");

    /// <summary>Tries to parse a version.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="version">The parsed version, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid version.</returns>
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim();
        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            core = core[..plus];
        }

        var dash = core.IndexOf('-', StringComparison.Ordinal);
        var prerelease = dash >= 0 ? core[(dash + 1)..] : "";
        if (dash >= 0)
        {
            core = core[..dash];
            if (prerelease.Split('.').Any(id => id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))
            {
                return false;
            }
        }

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch, prerelease);
        return true;
    }

    /// <summary>Compares two versions; a pre-release is lower than its release and numeric identifiers compare as numbers.</summary>
    /// <param name="other">The other version.</param>
    /// <returns>A negative, zero or positive number.</returns>
    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (core != 0)
        {
            return core;
        }

        if (IsPrerelease != other.IsPrerelease)
        {
            return IsPrerelease ? -1 : 1;
        }

        var left = Prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var right = other.Prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var result = CompareIdentifier(left[i], right[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    /// <summary>Returns whether <paramref name="left"/> is lower than <paramref name="right"/>.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>The comparison result.</returns>
    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>The comparison result.</returns>
    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    /// <summary>Returns whether <paramref name="left"/> is lower than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>The comparison result.</returns>
    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>The comparison result.</returns>
    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => IsPrerelease ? $"{Major}.{Minor}.{Patch}-{Prerelease}" : $"{Major}.{Minor}.{Patch}";

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = left.All(char.IsAsciiDigit);
        var rightNumeric = right.All(char.IsAsciiDigit);
        if (leftNumeric && rightNumeric)
        {
            // Digit strings compared without overflow: by length once leading zeros are gone, then by text.
            left = left.TrimStart('0');
            right = right.TrimStart('0');
            return left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
        }

        return leftNumeric != rightNumeric ? (leftNumeric ? -1 : 1) : string.CompareOrdinal(left, right);
    }
}
