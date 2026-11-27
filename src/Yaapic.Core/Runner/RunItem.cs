using Yaapic.Domain.Http;

namespace Yaapic.Core.Runner;

/// <summary>One request of a run, with the folder path it sits in.</summary>
/// <param name="Request">The request definition.</param>
/// <param name="Path">The folder names from the run folder (included) down to the request's parent, joined with <c>/</c>.</param>
public sealed record RunItem(HttpRequestDefinition Request, string Path);
