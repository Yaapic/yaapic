using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Builds <see cref="CodeRequest"/> values.</summary>
public static class CodeRequestFactory
{
    private const string FormType = "application/x-www-form-urlencoded";

    /// <summary>Converts a resolved request.</summary>
    /// <param name="request">The resolved request.</param>
    /// <returns>The code request.</returns>
    public static CodeRequest From(ResolvedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var headers = new List<KeyValueEntry>(request.Headers);
        var ct = request.BodyContentType;
        CodeBody body;
        var addContentType = true;
        if (request.Parts is { } parts)
        {
            body = new CodeBody.Multipart([.. parts.Select(p => new CodePart(p.Name, p.Value, p.FilePath))]);
            addContentType = false;
        }
        else if (request.BodyFilePath is { } path)
        {
            body = new CodeBody.File(path, ct);
        }
        else if (request.BodyText is { } text)
        {
            body = ct is not null && ct.StartsWith(FormType, StringComparison.OrdinalIgnoreCase)
                ? new CodeBody.Form(ParseForm(text))
                : new CodeBody.Text(text, ct);
        }
        else
        {
            body = new CodeBody.None();
            addContentType = false;
        }

        if (addContentType && !string.IsNullOrEmpty(ct)
            && !headers.Any(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new KeyValueEntry("Content-Type", ct));
        }

        string? note = null;
        switch (request.PendingAuth?.Kind)
        {
            case AuthKind.OAuth2 or AuthKind.Jwt:
                headers.Add(new KeyValueEntry("Authorization", "Bearer <access token>"));
                break;
            case AuthKind.Digest:
                note = "Digest authentication must be computed by the caller (RFC 7616).";
                break;
            case AuthKind.AwsV4:
                note = "AWS Signature v4 must be computed by the caller.";
                break;
        }

        return new CodeRequest(request.Method.ToText(), request.Url, headers, body, note);
    }

    private static List<KeyValueEntry> ParseForm(string text)
    {
        var fields = new List<KeyValueEntry>();
        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var name = eq < 0 ? pair : pair[..eq];
            var value = eq < 0 ? string.Empty : pair[(eq + 1)..];
            fields.Add(new KeyValueEntry(Decode(name), Decode(value)));
        }

        return fields;
    }

    private static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
}
