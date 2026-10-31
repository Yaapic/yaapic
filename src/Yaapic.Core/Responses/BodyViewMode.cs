namespace Yaapic.Core.Responses;

/// <summary>How a response body is presented.</summary>
public enum BodyViewMode
{
    /// <summary>Indented JSON or XML when the body parses.</summary>
    Pretty,

    /// <summary>The decoded text unchanged.</summary>
    Raw,
}
