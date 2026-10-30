using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Requests;

/// <summary>A saved request as shown in the sidebar list.</summary>
/// <param name="Id">The request identifier.</param>
/// <param name="Name">The request name.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The URL template.</param>
public sealed record RequestListItem(Guid Id, string Name, HttpMethodKind Method, string Url)
{
    /// <summary>Gets the upper-case method name, such as <c>GET</c>.</summary>
    public string MethodText => Method == HttpMethodKind.Post ? "POST" : "GET";
}
