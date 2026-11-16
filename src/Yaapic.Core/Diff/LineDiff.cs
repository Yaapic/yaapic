namespace Yaapic.Core.Diff;

/// <summary>A Myers O(ND) line diff.</summary>
public static class LineDiff
{
    /// <summary>The maximum combined line count accepted by <see cref="Compute"/>.</summary>
    public const int MaxTotalLines = 20_000;

    /// <summary>Computes the shortest edit script between two line lists.</summary>
    /// <param name="oldLines">The old lines.</param>
    /// <param name="newLines">The new lines.</param>
    /// <param name="ignoreWhitespace">Compares lines after trimming; the original text is still emitted.</param>
    /// <returns>The diff lines in order; in a changed block, removed lines come before added lines.</returns>
    /// <exception cref="ArgumentException">The combined line count exceeds <see cref="MaxTotalLines"/>.</exception>
    public static IReadOnlyList<DiffLine> Compute(
        IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines, bool ignoreWhitespace = false)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(newLines);
        if ((long)oldLines.Count + newLines.Count > MaxTotalLines)
        {
            throw new ArgumentException($"The inputs exceed {MaxTotalLines} lines in total.");
        }

        var a = Keys(oldLines, ignoreWhitespace);
        var b = Keys(newLines, ignoreWhitespace);
        var ops = Reorder(Script(a, b));

        var result = new List<DiffLine>(ops.Count);
        int o = 0, n = 0;
        foreach (var op in ops)
        {
            switch (op)
            {
                case DiffOp.Equal:
                    result.Add(new DiffLine(op, newLines[n], ++o, ++n));
                    break;
                case DiffOp.Removed:
                    result.Add(new DiffLine(op, oldLines[o], ++o, null));
                    break;
                default:
                    result.Add(new DiffLine(op, newLines[n], null, ++n));
                    break;
            }
        }

        return result;
    }

    /// <summary>Splits text on <c>\r\n</c>, <c>\n</c> and <c>\r</c>; a trailing terminator adds no empty line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines; empty for empty text.</returns>
    public static IReadOnlyList<string> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n')
            {
                continue;
            }

            lines.Add(text[start..i]);
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            start = i + 1;
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    /// <summary>Counts the added and removed lines of a diff.</summary>
    /// <param name="diff">The diff lines.</param>
    /// <returns>The counts.</returns>
    public static DiffStats Summarize(IReadOnlyList<DiffLine> diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        int added = 0, removed = 0;
        foreach (var line in diff)
        {
            if (line.Op == DiffOp.Added)
            {
                added++;
            }
            else if (line.Op == DiffOp.Removed)
            {
                removed++;
            }
        }

        return new DiffStats(added, removed, added + removed > 0);
    }

    private static string[] Keys(IReadOnlyList<string> lines, bool trim)
    {
        var keys = new string[lines.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = trim ? lines[i].Trim() : lines[i];
        }

        return keys;
    }

    // Within a block of changes, lists every removal before every addition (like git diff).
    private static List<DiffOp> Reorder(List<DiffOp> ops)
    {
        var result = new List<DiffOp>(ops.Count);
        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i] == DiffOp.Equal)
            {
                result.Add(ops[i++]);
                continue;
            }

            int removed = 0, added = 0;
            for (; i < ops.Count && ops[i] != DiffOp.Equal; i++)
            {
                if (ops[i] == DiffOp.Removed)
                {
                    removed++;
                }
                else
                {
                    added++;
                }
            }

            result.AddRange(Enumerable.Repeat(DiffOp.Removed, removed));
            result.AddRange(Enumerable.Repeat(DiffOp.Added, added));
        }

        return result;
    }

    private static List<DiffOp> Script(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length, max = n + m, offset = max + 1;
        var v = new int[(2 * max) + 3];
        var trace = new List<int[]>();
        var finalD = 0;

        for (var d = 0; d <= max; d++)
        {
            var done = false;
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1])
                    ? v[offset + k + 1]
                    : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    done = true;
                    break;
                }
            }

            // Snapshot of the diagonals -d-1..d+1, stored at index k + d + 1.
            trace.Add(v[(offset - d - 1)..(offset + d + 2)]);
            if (done)
            {
                finalD = d;
                break;
            }
        }

        var ops = new List<DiffOp>();
        int px = n, py = m;
        for (var d = finalD; d > 0; d--)
        {
            var prev = trace[d - 1];
            var k = px - py;
            var prevK = k == -d || (k != d && prev[k - 1 + d] < prev[k + 1 + d]) ? k + 1 : k - 1;
            var prevX = prev[prevK + d];
            var prevY = prevX - prevK;
            while (px > prevX && py > prevY)
            {
                ops.Add(DiffOp.Equal);
                px--;
                py--;
            }

            ops.Add(px == prevX ? DiffOp.Added : DiffOp.Removed);
            px = prevX;
            py = prevY;
        }

        for (; px > 0 && py > 0; px--, py--)
        {
            ops.Add(DiffOp.Equal);
        }

        ops.Reverse();
        return ops;
    }
}
