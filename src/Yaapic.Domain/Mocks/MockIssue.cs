namespace Yaapic.Domain.Mocks;

/// <summary>A problem found in a <see cref="MockServerDefinition"/>.</summary>
public enum MockIssue
{
    /// <summary>The server name is blank.</summary>
    NameEmpty,

    /// <summary>The port is outside 1024..65535.</summary>
    PortOutOfRange,

    /// <summary>The route method is not in <see cref="MockMethods.All"/>.</summary>
    RouteMethodInvalid,

    /// <summary>The route path is malformed.</summary>
    RoutePathInvalid,

    /// <summary>The route status is outside 100..599.</summary>
    RouteStatusOutOfRange,

    /// <summary>The route delay is outside 0..60000 ms.</summary>
    RouteDelayOutOfRange,

    /// <summary>An enabled response header name is not an HTTP token.</summary>
    RouteHeaderNameInvalid,
}
