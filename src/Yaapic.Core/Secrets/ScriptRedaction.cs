using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Secrets;

/// <summary>Applies an <see cref="ISecretRedactor"/> to script outputs.</summary>
public static class ScriptRedaction
{
    /// <summary>Redacts the message and origin of a console entry.</summary>
    /// <param name="redactor">The redactor.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The redacted entry.</returns>
    public static ConsoleEntry Redact(ISecretRedactor redactor, ConsoleEntry entry) =>
        entry with { Message = redactor.Redact(entry.Message), Origin = entry.Origin is null ? null : redactor.Redact(entry.Origin) };

    /// <summary>Redacts the name and message of a test result.</summary>
    /// <param name="redactor">The redactor.</param>
    /// <param name="test">The test result.</param>
    /// <returns>The redacted result.</returns>
    public static TestResult Redact(ISecretRedactor redactor, TestResult test) =>
        test with { Name = redactor.Redact(test.Name), Message = test.Message is null ? null : redactor.Redact(test.Message) };

    /// <summary>Redacts the message and stack of a script error.</summary>
    /// <param name="redactor">The redactor.</param>
    /// <param name="error">The error.</param>
    /// <returns>The redacted error.</returns>
    public static ScriptError Redact(ISecretRedactor redactor, ScriptError error) =>
        error with { Message = redactor.Redact(error.Message), Stack = error.Stack is null ? null : redactor.Redact(error.Stack) };

    /// <summary>Redacts the console, tests and error of a run.</summary>
    /// <param name="redactor">The redactor.</param>
    /// <param name="run">The run result.</param>
    /// <returns>The redacted run result.</returns>
    public static ScriptRunResult Redact(ISecretRedactor redactor, ScriptRunResult run) =>
        run with
        {
            Error = run.Error is null ? null : Redact(redactor, run.Error),
            Console = [.. run.Console.Select(c => Redact(redactor, c))],
            Tests = [.. run.Tests.Select(t => Redact(redactor, t))],
        };
}
