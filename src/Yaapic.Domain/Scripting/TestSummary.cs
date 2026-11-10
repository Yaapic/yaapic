namespace Yaapic.Domain.Scripting;

/// <summary>Counts and formats test results for the response "Tests" tab badge.</summary>
public static class TestSummary
{
    /// <summary>Counts passed and failed tests.</summary>
    /// <param name="tests">The test results.</param>
    /// <returns>The number of passed and failed tests.</returns>
    public static (int Passed, int Failed) Count(IEnumerable<TestResult> tests)
    {
        var passed = 0;
        var failed = 0;
        foreach (var test in tests)
        {
            if (test.Passed)
            {
                passed++;
            }
            else
            {
                failed++;
            }
        }

        return (passed, failed);
    }

    /// <summary>Formats the summary as "passed/total", or empty text when there are no tests.</summary>
    /// <param name="tests">The test results.</param>
    /// <returns>For example "4/6", or an empty string.</returns>
    public static string Format(IEnumerable<TestResult> tests)
    {
        var (passed, failed) = Count(tests);
        var total = passed + failed;
        return total == 0 ? "" : $"{passed}/{total}";
    }
}
