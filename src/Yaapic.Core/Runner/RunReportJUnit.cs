using System.Globalization;
using System.Text;
using System.Xml;
using Yaapic.Core.Export;

namespace Yaapic.Core.Runner;

/// <summary>Writes a <see cref="RunResult"/> as JUnit XML for CI tools (ADR 0018).</summary>
public static class RunReportJUnit
{
    /// <summary>Writes the report as UTF-8 XML without a BOM, two-space indentation and <c>\n</c> line ends.</summary>
    /// <param name="result">The run result.</param>
    /// <returns>The XML bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static byte[] Write(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var stamp = result.StartedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var suites = new List<Suite>();
        foreach (var it in result.Iterations)
        {
            foreach (var q in it.Requests) suites.Add(Suite.From(it.Index + 1, q, stamp));
        }

        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = new UTF8Encoding(false),
        };
        using (var w = XmlWriter.Create(stream, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("testsuites");
            w.WriteAttributeString("name", Clean(result.Name));
            w.WriteAttributeString("tests", Num(suites.Sum(static s => s.Cases.Count)));
            w.WriteAttributeString("failures", Num(suites.Sum(static s => s.Cases.Count(static c => c.Failure is not null))));
            w.WriteAttributeString("errors", Num(suites.Sum(static s => s.Cases.Count(static c => c.Error is not null))));
            w.WriteAttributeString("skipped", Num(suites.Sum(static s => s.Cases.Count(static c => c.Skipped))));
            w.WriteAttributeString("time", Seconds(result.Duration));
            foreach (var s in suites) WriteSuite(w, s);
            w.WriteEndElement();
            w.WriteEndDocument();
        }

        return stream.ToArray();
    }

    /// <summary>Builds the suggested file name: <c>{slug}-run-{yyyyMMdd-HHmmss}.xml</c> (UTC start).</summary>
    /// <param name="result">The run result.</param>
    /// <returns>The file name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static string SuggestedFileName(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var stamp = result.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{ExportPackager.SafeFileName(result.Name)}-run-{stamp}.xml";
    }

    private static void WriteSuite(XmlWriter w, Suite s)
    {
        var q = s.Request;
        w.WriteStartElement("testsuite");
        w.WriteAttributeString("name", Clean(string.Concat("Iteration ", Num(s.Iteration), " / ", s.ClassName)));
        w.WriteAttributeString("tests", Num(s.Cases.Count));
        w.WriteAttributeString("failures", Num(s.Cases.Count(static c => c.Failure is not null)));
        w.WriteAttributeString("errors", Num(s.Cases.Count(static c => c.Error is not null)));
        w.WriteAttributeString("skipped", Num(s.Cases.Count(static c => c.Skipped)));
        w.WriteAttributeString("time", Seconds(q.Duration));
        w.WriteAttributeString("timestamp", s.Timestamp);
        w.WriteStartElement("properties");
        Property(w, "method", q.Method.ToString().ToUpperInvariant());
        Property(w, "url", q.Url);
        if (q.StatusCode is { } code) Property(w, "status", Num(code));
        w.WriteEndElement();
        foreach (var c in s.Cases)
        {
            w.WriteStartElement("testcase");
            w.WriteAttributeString("name", Clean(c.Name));
            w.WriteAttributeString("classname", Clean(s.ClassName));
            w.WriteAttributeString("time", Seconds(c.Time));
            if (c.Failure is not null)
            {
                w.WriteStartElement("failure");
                w.WriteAttributeString("message", Clean(c.Failure));
                w.WriteEndElement();
            }
            else if (c.Error is not null)
            {
                w.WriteStartElement("error");
                w.WriteAttributeString("message", Clean(c.Error));
                w.WriteEndElement();
            }
            else if (c.Skipped)
            {
                w.WriteElementString("skipped", string.Empty);
            }

            w.WriteEndElement();
        }

        if (q.Console.Count > 0)
        {
            var lines = q.Console.Select(static e => $"[{e.Level.ToString().ToLowerInvariant()}] {e.Message}");
            w.WriteElementString("system-out", Clean(string.Join('\n', lines)));
        }

        w.WriteEndElement();
    }

    private static void Property(XmlWriter w, string name, string value)
    {
        w.WriteStartElement("property");
        w.WriteAttributeString("name", name);
        w.WriteAttributeString("value", Clean(value));
        w.WriteEndElement();
    }

    private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static string Seconds(TimeSpan t) => t.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>Replaces characters that XML 1.0 cannot carry (tab, CR and LF are kept).</summary>
    private static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var ok = c is '\t' or '\n' or '\r'
                || (c >= 0x20 && c <= 0xD7FF && !char.IsSurrogate(c))
                || (c >= 0xE000 && c <= 0xFFFD)
                || (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]));
            if (ok && char.IsHighSurrogate(c))
            {
                sb.Append(c).Append(text[++i]);
            }
            else
            {
                sb.Append(ok ? c : '?');
            }
        }

        return sb.ToString();
    }

    private sealed record Case(string Name, TimeSpan Time, string? Failure, string? Error, bool Skipped);

    private sealed record Suite(int Iteration, RunRequestResult Request, string ClassName, string Timestamp, List<Case> Cases)
    {
        public static Suite From(int iteration, RunRequestResult q, string stamp)
        {
            var className = q.Path.Length == 0 ? q.Name : $"{q.Path}/{q.Name}";
            var cases = new List<Case>();
            foreach (var t in q.Tests)
            {
                cases.Add(new Case(t.Name, t.Duration, t.Passed ? null : t.Message ?? "failed", null, false));
            }

            switch (q.Outcome)
            {
                case RunRequestOutcome.Error:
                    cases.Add(new Case("request", q.Duration, null, q.Error ?? "failed", false));
                    break;
                case RunRequestOutcome.Skipped:
                    cases.Add(new Case("request", TimeSpan.Zero, null, null, true));
                    break;
                case RunRequestOutcome.Passed when cases.Count == 0:
                    cases.Add(new Case("request", q.Duration, null, null, false));
                    break;
            }

            return new Suite(iteration, q, className, stamp, cases);
        }
    }
}
