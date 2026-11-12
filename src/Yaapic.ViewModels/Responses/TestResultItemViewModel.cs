using System.Globalization;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Responses;

/// <summary>Display-ready form of one test result.</summary>
public sealed class TestResultItemViewModel
{
    /// <summary>Initializes a new instance of the <see cref="TestResultItemViewModel"/> class.</summary>
    /// <param name="result">The test result to display.</param>
    public TestResultItemViewModel(TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Name = result.Name;
        Passed = result.Passed;
        Message = result.Message;
        DurationText = ((long)Math.Round(result.Duration.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture) + " ms";
        Origin = result.Origin;
    }

    /// <summary>Gets the test name.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether the test passed.</summary>
    public bool Passed { get; }

    /// <summary>Gets the failure message, or <see langword="null"/>.</summary>
    public string? Message { get; }

    /// <summary>Gets the duration, such as <c>3 ms</c>.</summary>
    public string DurationText { get; }

    /// <summary>Gets the owner of the script that ran the test.</summary>
    public string Origin { get; }

    /// <summary>Gets <c>Passed</c> or <c>Failed</c>.</summary>
    public string StatusText => Passed ? Strings.TestsPassed : Strings.TestsFailed;

    /// <summary>Gets the status glyph: a check mark or a cross.</summary>
    public string Symbol => Passed ? "✓" : "×";
}
