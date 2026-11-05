namespace Yaapic.Core.Search;

/// <summary>Deterministic subsequence fuzzy matcher used by the command palette.</summary>
public static class FuzzyMatcher
{
    /// <summary>Matches <paramref name="query"/> against <paramref name="text"/>.</summary>
    /// <param name="query">The user query; null, empty or whitespace matches everything.</param>
    /// <param name="text">The candidate text.</param>
    /// <returns>The match, or null when the query is not a case-insensitive subsequence of the text.</returns>
    public static FuzzyMatch? Match(string? query, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(query))
        {
            return new FuzzyMatch(0, []);
        }

        query = query.Trim();
        var indices = new List<int>(query.Length);
        var score = 0;
        var pos = 0;
        var previous = -1;
        foreach (var qc in query)
        {
            var idx = -1;
            for (var i = pos; i < text.Length; i++)
            {
                if (char.ToUpperInvariant(text[i]) == char.ToUpperInvariant(qc))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                return null;
            }

            score += 1;
            if (idx == 0)
            {
                score += 8;
            }
            else if (IsWordStart(text, idx))
            {
                score += 6;
            }

            if (previous >= 0)
            {
                score += idx == previous + 1 ? 4 : -(idx - previous - 1);
            }

            indices.Add(idx);
            previous = idx;
            pos = idx + 1;
        }

        return new FuzzyMatch(score, indices);
    }

    /// <summary>Orders matches: higher score first, then shorter text, then ordinal text.</summary>
    /// <param name="a">First match.</param>
    /// <param name="textA">Text of the first match.</param>
    /// <param name="b">Second match.</param>
    /// <param name="textB">Text of the second match.</param>
    /// <returns>A negative value when <paramref name="a"/> ranks first, positive when <paramref name="b"/> does, else 0.</returns>
    public static int Compare(FuzzyMatch a, string textA, FuzzyMatch b, string textB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0)
        {
            return byScore;
        }

        var byLength = textA.Length.CompareTo(textB.Length);
        return byLength != 0 ? byLength : string.CompareOrdinal(textA, textB);
    }

    private static bool IsWordStart(string text, int i)
    {
        var p = text[i - 1];
        return p is ' ' or '-' or '_' or '/' or '.' or ':' || (char.IsUpper(text[i]) && char.IsLower(p));
    }
}
