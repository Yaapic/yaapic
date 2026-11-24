using System.Globalization;
using System.Text;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Curl;

/// <summary>Splits pasted text into <c>curl</c> commands and each command into shell words (a bash subset).</summary>
public static class CurlTokenizer
{
    /// <summary>Tokenizes the text.</summary>
    /// <param name="text">The pasted text.</param>
    /// <returns>One word list per <c>curl</c> command; empty when the text has none.</returns>
    /// <exception cref="FormatException">A quote is not closed, or the text uses the Windows cmd <c>^"</c> form.</exception>
    public static IReadOnlyList<IReadOnlyList<string>> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var commands = new List<IReadOnlyList<string>>();
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        var lineStart = true;

        void EndWord()
        {
            if (inWord)
            {
                words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
        }

        void EndCommand()
        {
            EndWord();
            if (words.Count > 0 && IsCurl(words[0]))
            {
                commands.Add(words.ToArray());
            }

            words.Clear();
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (lineStart && !inWord && words.Count == 0 && (c == '$' || c == '>') && i + 1 < text.Length && text[i + 1] == ' ')
            {
                i += 2;
                continue;
            }

            if (c is not (' ' or '\t'))
            {
                lineStart = false;
            }

            switch (c)
            {
                case ' ' or '\t':
                    EndWord();
                    i++;
                    break;
                case '\n':
                    EndCommand();
                    lineStart = true;
                    i++;
                    break;
                case '#' when !inWord:
                    while (i < text.Length && text[i] != '\n')
                    {
                        i++;
                    }

                    break;
                case '\\':
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else if (i + 1 < text.Length)
                    {
                        word.Append(text[i + 1]);
                        inWord = true;
                        i += 2;
                    }
                    else
                    {
                        word.Append('\\');
                        inWord = true;
                        i++;
                    }

                    break;
                case '^' when i + 1 < text.Length && text[i + 1] == '\n':
                    i += 2;
                    break;
                case '^' when i + 1 < text.Length && text[i + 1] == '"':
                    if (IsCurl(words.Count > 0 ? words[0] : word.ToString()))
                    {
                        throw new FormatException(Strings.ImportCurlCmdSyntax);
                    }

                    word.Append(c);
                    inWord = true;
                    i++;
                    break;
                case '\'':
                    i = ReadUntil(text, i + 1, '\'', word);
                    inWord = true;
                    break;
                case '"':
                    i = ReadDoubleQuoted(text, i + 1, word);
                    inWord = true;
                    break;
                case '$' when i + 1 < text.Length && text[i + 1] == '\'':
                    i = ReadAnsiC(text, i + 2, word);
                    inWord = true;
                    break;
                case ';' when NextWordIsCurl(text, i + 1):
                    EndCommand();
                    i++;
                    break;
                case '&' when i + 1 < text.Length && text[i + 1] == '&' && NextWordIsCurl(text, i + 2):
                    EndCommand();
                    i += 2;
                    break;
                default:
                    word.Append(c);
                    inWord = true;
                    i++;
                    break;
            }
        }

        EndCommand();
        return commands;
    }

    private static bool IsCurl(string word) => word is "curl" or "curl.exe";

    private static bool NextWordIsCurl(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        var end = index;
        while (end < text.Length && text[end] is not (' ' or '\t' or '\n'))
        {
            end++;
        }

        return IsCurl(text[index..end]);
    }

    private static int ReadUntil(string text, int index, char quote, StringBuilder word)
    {
        var end = text.IndexOf(quote, index);
        if (end < 0)
        {
            throw new FormatException(Strings.ImportCurlUnterminatedQuote);
        }

        word.Append(text, index, end - index);
        return end + 1;
    }

    private static int ReadDoubleQuoted(string text, int index, StringBuilder word)
    {
        while (index < text.Length)
        {
            var c = text[index];
            if (c == '"')
            {
                return index + 1;
            }

            if (c == '\\' && index + 1 < text.Length)
            {
                var next = text[index + 1];
                if (next == '\n')
                {
                    index += 2;
                    continue;
                }

                if (next is '"' or '\\' or '$' or '`')
                {
                    word.Append(next);
                    index += 2;
                    continue;
                }
            }

            word.Append(c);
            index++;
        }

        throw new FormatException(Strings.ImportCurlUnterminatedQuote);
    }

    private static int ReadAnsiC(string text, int index, StringBuilder word)
    {
        while (index < text.Length)
        {
            var c = text[index];
            if (c == '\'')
            {
                return index + 1;
            }

            if (c != '\\' || index + 1 >= text.Length)
            {
                word.Append(c);
                index++;
                continue;
            }

            var e = text[index + 1];
            index += 2;
            switch (e)
            {
                case 'n': word.Append('\n'); break;
                case 'r': word.Append('\r'); break;
                case 't': word.Append('\t'); break;
                case 'a': word.Append('\a'); break;
                case 'b': word.Append('\b'); break;
                case 'f': word.Append('\f'); break;
                case 'v': word.Append('\v'); break;
                case 'e': word.Append('\u001b'); break;
                case '\\' or '\'' or '"': word.Append(e); break;
                case 'x' or 'u' or 'U':
                    var n = CountDigits(text, index, e == 'x' ? 2 : e == 'u' ? 4 : 8, hex: true);
                    if (n == 0)
                    {
                        word.Append('\\').Append(e);
                        break;
                    }

                    AppendCodePoint(word, int.Parse(text.AsSpan(index, n), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture), text.Substring(index - 2, n + 2));
                    index += n;
                    break;
                case >= '0' and <= '7':
                    var count = 1 + CountDigits(text, index, 2, hex: false);
                    word.Append((char)Convert.ToInt32(text.Substring(index - 1, count), 8));
                    index += count - 1;
                    break;
                default:
                    word.Append('\\').Append(e);
                    break;
            }
        }

        throw new FormatException(Strings.ImportCurlUnterminatedQuote);
    }

    private static int CountDigits(string text, int index, int max, bool hex)
    {
        var n = 0;
        while (n < max && index + n < text.Length && (hex ? char.IsAsciiHexDigit(text[index + n]) : text[index + n] is >= '0' and <= '7'))
        {
            n++;
        }

        return n;
    }

    private static void AppendCodePoint(StringBuilder word, int value, string raw)
    {
        if (value is > 0x10FFFF or (>= 0xD800 and <= 0xDFFF))
        {
            word.Append(raw);
        }
        else
        {
            word.Append(char.ConvertFromUtf32(value));
        }
    }
}
