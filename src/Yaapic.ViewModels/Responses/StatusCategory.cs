namespace Yaapic.ViewModels.Responses;

/// <summary>The class of an HTTP status code, used to colour the status badge.</summary>
public enum StatusCategory
{
    /// <summary>1xx, and any code outside 100 to 599.</summary>
    Informational,

    /// <summary>2xx.</summary>
    Success,

    /// <summary>3xx.</summary>
    Redirect,

    /// <summary>4xx.</summary>
    ClientError,

    /// <summary>5xx.</summary>
    ServerError,
}
