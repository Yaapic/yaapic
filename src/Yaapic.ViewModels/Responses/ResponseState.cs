namespace Yaapic.ViewModels.Responses;

/// <summary>What the response panel is currently showing.</summary>
public enum ResponseState
{
    /// <summary>No request has been sent yet, or the panel was cleared.</summary>
    Empty,

    /// <summary>A request is in flight.</summary>
    Sending,

    /// <summary>A response was received.</summary>
    Completed,

    /// <summary>The request produced no response.</summary>
    Failed,
}
