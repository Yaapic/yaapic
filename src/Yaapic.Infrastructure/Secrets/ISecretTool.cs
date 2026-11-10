namespace Yaapic.Infrastructure.Secrets;

/// <summary>The result of one <c>secret-tool</c> run.</summary>
/// <param name="ExitCode">The exit code, or -1 when the tool could not start or timed out.</param>
/// <param name="Output">Standard output.</param>
internal sealed record SecretToolResult(int ExitCode, string Output);

/// <summary>Runs <c>secret-tool</c>, so the protector logic can be tested without D-Bus.</summary>
internal interface ISecretTool
{
    /// <summary>Tells whether <c>secret-tool</c> is on the <c>PATH</c>.</summary>
    /// <returns><see langword="true"/> when found.</returns>
    bool Exists();

    /// <summary>Runs the tool; <paramref name="stdin"/> goes to standard input, never to arguments.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="stdin">The standard input text, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    SecretToolResult Run(IReadOnlyList<string> arguments, string? stdin);
}
