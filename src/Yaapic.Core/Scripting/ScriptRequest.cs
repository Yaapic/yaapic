using System.Collections.ObjectModel;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>The request as scripts see and mutate it.</summary>
public sealed class ScriptRequest
{
    private string _method = "GET";
    private string _url = "";
    private string _bodyMode = "none";
    private string _bodyText = "";
    private string _bodyContentType = "text";
    private IReadOnlyList<KeyValueEntry> _disabledParams = [];

    /// <summary>Initializes an empty, mutable GET request.</summary>
    public ScriptRequest()
    {
        Headers = new GuardedList(this);
        UrlEncoded = new GuardedList(this);
    }

    /// <summary>Gets or sets the upper-case method text.</summary>
    public string Method { get => _method; set => Set(out _method, value); }

    /// <summary>Gets or sets the full URL, query included, variables untouched.</summary>
    public string Url { get => _url; set => Set(out _url, value); }

    /// <summary>Gets the headers.</summary>
    public IList<KeyValueEntry> Headers { get; }

    /// <summary>Gets or sets the body mode: <c>none</c>, <c>raw</c>, <c>urlencoded</c>, <c>formdata</c> or <c>binary</c>.</summary>
    public string BodyMode { get => _bodyMode; set => Set(out _bodyMode, value); }

    /// <summary>Gets or sets the raw body text.</summary>
    public string BodyText { get => _bodyText; set => Set(out _bodyText, value); }

    /// <summary>Gets or sets the raw content type text, such as <c>json</c>.</summary>
    public string BodyContentType { get => _bodyContentType; set => Set(out _bodyContentType, value); }

    /// <summary>Gets the url-encoded form entries.</summary>
    public IList<KeyValueEntry> UrlEncoded { get; }

    /// <summary>Gets the multipart form entries (read-only).</summary>
    public IReadOnlyList<KeyValueEntry> FormData { get; private set; } = [];

    /// <summary>Gets a value indicating whether every mutation throws.</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>Snapshots a request definition.</summary>
    /// <param name="definition">The definition to read.</param>
    /// <param name="readOnly">Whether the snapshot rejects mutations.</param>
    /// <returns>The snapshot.</returns>
    public static ScriptRequest FromDefinition(HttpRequestDefinition definition, bool readOnly)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var parts = QueryStringSync.Parse(definition.Url);
        List<KeyValueEntry> query = [.. parts.Params, .. definition.QueryParams.Where(p => p.Enabled)];
        var body = definition.Body;
        ScriptRequest r = new()
        {
            _method = definition.Method.ToText(),
            _url = QueryStringSync.Compose(parts.Prefix, query, parts.Fragment),
            _bodyMode = body.Kind switch
            {
                BodyKind.Raw => "raw",
                BodyKind.UrlEncoded => "urlencoded",
                BodyKind.FormData => "formdata",
                BodyKind.Binary => "binary",
                _ => "none",
            },
            _bodyText = body.Text,
            _bodyContentType = body.ContentType.ToString().ToLowerInvariant(),
            _disabledParams = [.. definition.QueryParams.Where(p => !p.Enabled)],
        };
        foreach (var h in definition.Headers)
        {
            r.Headers.Add(h);
        }

        var fields = body.Fields.Select(f => new KeyValueEntry(f.Key, f.Value, f.Enabled)).ToList();
        if (body.Kind == BodyKind.UrlEncoded)
        {
            foreach (var f in fields)
            {
                r.UrlEncoded.Add(f);
            }
        }
        else if (body.Kind == BodyKind.FormData)
        {
            r.FormData = fields;
        }

        r.IsReadOnly = readOnly;
        return r;
    }

    /// <summary>Applies the script edits to the original definition.</summary>
    /// <param name="original">The definition the snapshot came from.</param>
    /// <returns>A new definition; auth, settings and untouched bodies are kept.</returns>
    /// <exception cref="InvalidOperationException">The method text is unknown.</exception>
    public HttpRequestDefinition ApplyTo(HttpRequestDefinition original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!HttpMethodKindExtensions.TryParse(Method, out var method))
        {
            throw new InvalidOperationException($"Unknown HTTP method '{Method}'");
        }

        var parts = QueryStringSync.Parse(Url);
        var body = original.Body;
        if (BodyMode == "raw")
        {
            var type = Enum.TryParse<RawContentType>(BodyContentType, true, out var t) ? t : body.ContentType;
            body = RequestBody.Raw(type, BodyText);
        }
        else if (BodyMode == "urlencoded")
        {
            body = RequestBody.UrlEncoded([.. UrlEncoded.Select(e => new FormField(e.Key, e.Value, e.Enabled))]);
        }

        return original with
        {
            Method = method,
            Url = parts.Prefix + parts.Fragment,
            QueryParams = [.. parts.Params, .. _disabledParams],
            Headers = [.. Headers],
            Body = body,
        };
    }

    private void Set(out string field, string value)
    {
        CheckWritable();
        field = value;
    }

    private void CheckWritable()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("yaapic.request is read-only in post-response scripts");
        }
    }

    private sealed class GuardedList(ScriptRequest owner) : Collection<KeyValueEntry>
    {
        protected override void InsertItem(int index, KeyValueEntry item)
        {
            owner.CheckWritable();
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, KeyValueEntry item)
        {
            owner.CheckWritable();
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            owner.CheckWritable();
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            owner.CheckWritable();
            base.ClearItems();
        }
    }
}
