using System.ComponentModel;
using System.Diagnostics;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Runs the real <c>secret-tool</c> process with a 5 second timeout.</summary>
internal sealed class ProcessSecretTool : ISecretTool
{
    private const string Tool = "secret-tool";

    public bool Exists() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, Tool)));

    public SecretToolResult Run(IReadOnlyList<string> arguments, string? stdin)
    {
        var info = new ProcessStartInfo(Tool)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return new SecretToolResult(-1, "");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
            }

            process.StandardInput.Close();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return new SecretToolResult(-1, "");
            }

            return new SecretToolResult(process.ExitCode, output.GetAwaiter().GetResult());
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return new SecretToolResult(-1, "");
        }
    }
}
