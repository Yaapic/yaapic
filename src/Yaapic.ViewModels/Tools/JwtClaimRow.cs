namespace Yaapic.ViewModels.Tools;

/// <summary>A time claim of a decoded JWT.</summary>
/// <param name="Name">The claim name: <c>iat</c>, <c>nbf</c> or <c>exp</c>.</param>
/// <param name="LocalTimeText">The claim as a local date and time.</param>
/// <param name="RelativeText">The distance from now, such as <c>in 2 h</c> or <c>3 days ago</c>.</param>
public sealed record JwtClaimRow(string Name, string LocalTimeText, string RelativeText);

/// <summary>The time-based state shown for a pasted token.</summary>
public enum JwtStatusKind
{
    /// <summary>Nothing to report: empty or undecodable input.</summary>
    None,

    /// <summary>The token has an expiry in the future and is already usable.</summary>
    Valid,

    /// <summary>The token has expired.</summary>
    Expired,

    /// <summary>The token's <c>nbf</c> is in the future.</summary>
    NotYetValid,

    /// <summary>The token has no <c>exp</c> claim.</summary>
    NoExpiry,
}
