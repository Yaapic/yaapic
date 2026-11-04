using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Requests;

/// <summary>Describes a send that completed, successfully or not, without being canceled.</summary>
/// <param name="request">The request exactly as it was sent, variables unresolved.</param>
/// <param name="report">The outcome of the send.</param>
public sealed class RequestSentEventArgs(HttpRequestDefinition request, SendReport report) : EventArgs
{
    /// <summary>Gets the request as sent.</summary>
    public HttpRequestDefinition Request { get; } = request;

    /// <summary>Gets the outcome of the send.</summary>
    public SendReport Report { get; } = report;
}
