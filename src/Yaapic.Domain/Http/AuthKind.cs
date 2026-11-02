namespace Yaapic.Domain.Http;

/// <summary>How a request authenticates.</summary>
public enum AuthKind
{
    /// <summary>No authentication.</summary>
    None,

    /// <summary>HTTP Basic with a username and password.</summary>
    Basic,

    /// <summary>A bearer token in the Authorization header.</summary>
    Bearer,

    /// <summary>A named API key sent in a header or the query string.</summary>
    ApiKey,
}
