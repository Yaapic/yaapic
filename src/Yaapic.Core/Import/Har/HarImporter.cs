using System.Text;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Har;

/// <summary>Turns the <c>log.entries</c> of a HAR file into requests in one folder.</summary>
public sealed class HarImporter : IImporter
{
    private const int SniffLength = 8192;

    /// <inheritdoc />
    public string Id => "har";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportHarDisplayName;

    /// <inheritdoc />
    public ImportConfidence Sniff(ImportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Files.Count == 0)
        {
            return ImportConfidence.None;
        }

        var text = input.Files[0].Text;
        var head = text.Length > SniffLength ? text[..SniffLength] : text;
        if (head.Contains("\"entries\"", StringComparison.Ordinal))
        {
            if (head.Contains("\"log\"", StringComparison.Ordinal))
            {
                return ImportConfidence.Certain;
            }

            if (head.Contains("\"request\"", StringComparison.Ordinal))
            {
                return ImportConfidence.Possible;
            }
        }

        return ImportConfidence.None;
    }

    /// <inheritdoc />
    public ImportBundle Import(ImportInput input, ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        DataNode root;
        try
        {
            root = DataNodeJson.Parse(input.Single.Text);
        }
        catch (FormatException ex)
        {
            throw new ImportException(ex.Message, ex);
        }

        if (root["log"]?["entries"] is not { IsArray: true } entries)
        {
            throw new ImportException(Strings.ImportHarNotAHar);
        }

        List<ImportItem> children = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        var skipped = new int[Enum.GetValues<HarSkipReason>().Length];
        var duplicates = 0;
        var withResponse = 0;
        for (var i = 0; i < entries.Items.Count; i++)
        {
            var entry = entries.Items[i];
            if (entry["response"] is { IsObject: true })
            {
                withResponse++;
            }

            if (HarFilter.ShouldSkip(entry, context.Options, out var reason))
            {
                skipped[(int)reason]++;
                continue;
            }

            if (HarEntries.Convert(entry, i, context) is not { } request)
            {
                continue;
            }

            if (seen.Add(RequestKey(request)))
            {
                children.Add(new ImportRequest(request));
            }
            else
            {
                duplicates++;
            }
        }

        ReportCount(context, skipped[(int)HarSkipReason.Static], Strings.ImportHarSkippedStatic);
        ReportCount(context, skipped[(int)HarSkipReason.Preflight], Strings.ImportHarSkippedPreflight);
        ReportCount(context, skipped[(int)HarSkipReason.Socket], Strings.ImportHarSkippedSocket);
        ReportCount(context, skipped[(int)HarSkipReason.DataUrl], Strings.ImportHarSkippedDataUrl);
        ReportCount(context, duplicates, Strings.ImportHarDuplicates);

        if (withResponse > 0)
        {
            context.Report.Info(string.Empty, Strings.ImportHarResponsesIgnored(withResponse));
        }

        var folder = new FolderDefinition(context.NewId(), ImportHelpers.SafeName(input.Name, "HAR import"), "", [], ScriptSet.Empty);
        return new ImportBundle(new ImportFolder(folder, children), [], [], [], context.Report);
    }

    private static void ReportCount(ImportContext context, int count, Func<object?, string> message)
    {
        if (count > 0)
        {
            context.Report.Info(string.Empty, message(count));
        }
    }

    // Canonical key: method, url, query, headers (name ignoring case, value, order) and body; the 0x1F separator cannot occur in a header or url.
    private static string RequestKey(HttpRequestDefinition request)
    {
        var sb = new StringBuilder();
        sb.Append(request.Method).Append('').Append(request.Url);
        foreach (var q in request.QueryParams)
        {
            sb.Append('').Append('q').Append(q.Enabled).Append(q.Key).Append('=').Append(q.Value);
        }

        foreach (var h in request.Headers.Where(h => h.Enabled))
        {
            sb.Append('').Append('h').Append(h.Key.ToLowerInvariant()).Append(':').Append(h.Value);
        }

        var body = request.Body;
        sb.Append('').Append('b').Append(body.Kind).Append(body.ContentType).Append(':').Append(body.Text).Append(body.FilePath);
        foreach (var f in body.Fields)
        {
            sb.Append('').Append('f').Append(f.Enabled).Append(f.Kind).Append(f.Key).Append('=').Append(f.Value);
        }

        return sb.ToString();
    }
}
