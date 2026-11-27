using System.Text;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Runner;

/// <summary>Parses CSV text (RFC 4180, comma separator) into iteration data rows.</summary>
public static class CsvIterationData
{
    /// <summary>Parses decoded CSV text; the first record is the header.</summary>
    /// <param name="text">The text, already decoded (no byte order mark).</param>
    /// <returns>One row per data record; empty when there is only a header.</returns>
    /// <exception cref="IterationDataException">A record has too many fields, a quote is never closed, or a limit is exceeded.</exception>
    public static IReadOnlyList<IterationDataRow> Parse(string text)
    {
        List<(List<string> Fields, int Line)> records = ReadRecords(text);
        if (records.Count == 0)
        {
            return [];
        }

        string[] header = BuildHeader(records[0].Fields);
        if (records.Count - 1 > IterationDataLimits.MaxRows)
        {
            throw new IterationDataException(Strings.RunnerDataTooLarge);
        }

        List<IterationDataRow> rows = new(records.Count - 1);
        for (int r = 1; r < records.Count; r++)
        {
            (List<string> fields, int line) = records[r];
            if (fields.Count > header.Length)
            {
                throw new IterationDataException(Strings.RunnerDataTooManyFields(line));
            }

            KeyValueEntry[] values = new KeyValueEntry[header.Length];
            for (int c = 0; c < header.Length; c++)
            {
                values[c] = new KeyValueEntry(header[c], c < fields.Count ? fields[c] : "");
            }

            rows.Add(new IterationDataRow(values));
        }

        return rows;
    }

    private static string[] BuildHeader(List<string> raw)
    {
        if (raw.Count > IterationDataLimits.MaxColumns)
        {
            throw new IterationDataException(Strings.RunnerDataTooLarge);
        }

        string[] names = new string[raw.Count];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            string name = raw[i].Trim();
            if (name.Length == 0)
            {
                name = $"column{i + 1}";
            }
            else if (seen.Contains(name))
            {
                name = $"{name}_{i + 1}";
            }

            seen.Add(name);
            names[i] = name;
        }

        return names;
    }

    // Single pass; a record's line is the 1-based physical line where it starts. Blank lines are skipped.
    private static List<(List<string> Fields, int Line)> ReadRecords(string text)
    {
        List<(List<string>, int)> records = [];
        List<string> fields = [];
        StringBuilder field = new();
        int line = 1;
        int recordLine = 1;
        bool quoted = false;
        bool wasQuoted = false;
        int quoteLine = 0;

        void EndRecord()
        {
            // A record made of one empty unquoted field is a blank line.
            if (!(fields.Count == 0 && field.Length == 0 && !wasQuoted))
            {
                fields.Add(field.ToString());
                records.Add((fields, recordLine));
                if (records.Count > IterationDataLimits.MaxRows + 1)
                {
                    throw new IterationDataException(Strings.RunnerDataTooLarge);
                }
            }

            fields = [];
            field.Clear();
            wasQuoted = false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                    if (ch == '\n' || (ch == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                    {
                        line++;
                    }
                }

                continue;
            }

            switch (ch)
            {
                case '"' when field.Length == 0 && !wasQuoted:
                    quoted = true;
                    wasQuoted = true;
                    quoteLine = line;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    wasQuoted = false;
                    break;
                case '\r' or '\n':
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (quoted)
        {
            throw new IterationDataException(Strings.RunnerDataUnclosedQuote(quoteLine));
        }

        EndRecord();
        return records;
    }
}
