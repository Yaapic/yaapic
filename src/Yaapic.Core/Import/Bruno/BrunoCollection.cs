using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>Walks the files of a Bruno collection and builds the folder tree.</summary>
internal sealed class BrunoCollection(ImportInput input, ImportContext context)
{
    private const int MaxDepth = 32;
    private const string Manifest = "bruno.json";
    private const string FolderFileName = "folder.bru";
    private const string CollectionFileName = "collection.bru";

    private readonly ImportReport _report = context.Report;
    private int _headerCopies;
    private int _authCopies;
    private int _scripts;
    private bool _hasSeq;

    /// <summary>Tells whether a path is a <c>bruno.json</c> manifest.</summary>
    /// <param name="path">The relative path.</param>
    /// <returns>True for the manifest.</returns>
    internal static bool IsManifest(string path) =>
        path.Equals(Manifest, StringComparison.Ordinal) || path.EndsWith("/" + Manifest, StringComparison.Ordinal);

    /// <summary>Tells whether a path is a <c>.bru</c> file.</summary>
    /// <param name="path">The relative path.</param>
    /// <returns>True for a Bru file.</returns>
    internal static bool IsBru(string path) => path.EndsWith(".bru", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the whole input.</summary>
    /// <returns>The import result.</returns>
    internal ImportBundle Read()
    {
        var manifest = input.Files.Where(f => IsManifest(f.Path)).MinBy(f => f.Path.Length);
        if (manifest is null && input.Files.Count == 1 && IsBru(input.Files[0].Path))
        {
            return ReadSingle(input.Files[0]);
        }

        var root = manifest is null ? "" : manifest.Path[..^Manifest.Length];
        var (name, ignored) = ReadManifest(manifest);

        var tree = new Dir("", true);
        foreach (var file in input.Files)
        {
            if (file.Path.StartsWith(root, StringComparison.Ordinal) && IsBru(file.Path)
                && Relative(file.Path[root.Length..], ignored) is { } parts)
            {
                tree.Add(parts, file);
            }
        }

        var collectionFile = tree.FolderFile is { } c ? Parse(c) : null;
        NoteSeq(collectionFile);
        var collectionScripts = Scripts(collectionFile, "");
        var auth = Auth(collectionFile, "", RequestAuth.None);
        var children = ReadDir(tree, [], Headers(collectionFile), auth, 1);

        if (_headerCopies > 0)
        {
            _report.Info("", Strings.ImportBrunoHeadersInherited(_headerCopies));
        }

        if (_authCopies > 0)
        {
            _report.Info("", Strings.ImportAuthInherited(_authCopies));
        }

        if (_hasSeq)
        {
            _report.Info("", Strings.ImportOrderLost);
        }

        NoteScripts();
        var folder = new FolderDefinition(context.NewId(), ImportHelpers.SafeName(name, input.Name), "", [], collectionScripts);
        var environments = BrunoEnvironments.Map(input, root, folder.Id, context.NewId, _report);
        return new ImportBundle(new ImportFolder(folder, children), [], environments, [], _report);
    }

    private ScriptSet Scripts(BruFile? file, string path)
    {
        var set = file is null ? ScriptSet.Empty : BrunoScripts.Map(file, path, _report);
        _scripts += BrunoScripts.Count(set);
        return set;
    }

    private void NoteScripts()
    {
        if (_scripts > 0)
        {
            _report.Warning("", Strings.ImportScriptsReview(_scripts));
        }
    }

    private static BruFile Parse(ImportFile file) => BruParser.Parse(file.Text);

    private static string? Value(BruBlock? block, string key) => block?.Pairs.FirstOrDefault(p => p.Key == key)?.Value;

    private static List<KeyValueEntry> Headers(BruFile? file) =>
        [.. (file?.Get("headers")?.Pairs ?? []).Select(p => new KeyValueEntry(p.Key, p.Value, p.Enabled))];

    // Returns the path parts below the root, or null when the file is skipped (ignored directory, environments).
    private static string[]? Relative(string relative, HashSet<string> ignored)
    {
        var parts = relative.Split('/');
        var dirs = parts[..^1];
        if (dirs.Any(d => d is "node_modules" or ".git" || ignored.Contains(d))
            || ignored.Contains(string.Join('/', dirs))
            || (dirs.Length >= 1 && dirs[0] == "environments"))
        {
            return null;
        }

        return parts;
    }

    // Appends own headers after the inherited ones; an own header replaces an inherited one of the same name.
    private static List<KeyValueEntry> Merge(List<KeyValueEntry> inherited, List<KeyValueEntry> own) =>
        [.. inherited.Where(h => !own.Exists(o => o.Key.Equals(h.Key, StringComparison.OrdinalIgnoreCase))), .. own];

    private void NoteSeq(BruFile? file)
    {
        if (file?.Get("meta")?.Pairs.Any(p => p.Key == "seq") == true)
        {
            _hasSeq = true;
        }
    }

    private ImportBundle ReadSingle(ImportFile file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Path);
        var bru = Parse(file);
        NoteSeq(bru);
        var request = BrunoRequests.Map(bru, name, context.NewId(), _report, out _);
        if (request is null)
        {
            return ImportBundle.Empty(_report);
        }

        if (_hasSeq)
        {
            _report.Info("", Strings.ImportOrderLost);
        }

        _scripts += BrunoScripts.Count(request.Scripts);
        NoteScripts();
        return new ImportBundle(null, [request], [], [], _report);
    }

    private (string? Name, HashSet<string> Ignored) ReadManifest(ImportFile? manifest)
    {
        if (manifest is null)
        {
            return (null, []);
        }

        try
        {
            var node = DataNodeJson.Parse(manifest.Text);
            var ignored = (node["ignore"]?.Items ?? []).Select(i => i.AsString()?.Trim('/') ?? "").Where(s => s.Length > 0);
            return (node.GetString("name"), [.. ignored]);
        }
        catch (FormatException ex)
        {
            _report.Warning(manifest.Path, Strings.ImportInvalidJson(ex.Message));
            return (null, []);
        }
    }

    private List<ImportItem> ReadDir(Dir dir, string[] names, List<KeyValueEntry> headers, RequestAuth auth, int depth)
    {
        List<ImportItem> items = [];
        foreach (var sub in dir.Subs.Where(_ => depth < MaxDepth))
        {
            var folderFile = sub.FolderFile is { } f ? Parse(f) : null;
            NoteSeq(folderFile);
            var folderName = ImportHelpers.SafeName(Value(folderFile?.Get("meta"), "name"), sub.Name);
            string[] path = [.. names, folderName];
            var folderScripts = Scripts(folderFile, ImportPath.Join(path));
            var children = ReadDir(sub, path, Merge(headers, Headers(folderFile)), Auth(folderFile, ImportPath.Join(path), auth), depth + 1);
            if (children.Count > 0)
            {
                items.Add(new ImportFolder(new FolderDefinition(context.NewId(), folderName, "", [], folderScripts), children));
            }
        }

        foreach (var file in dir.Requests)
        {
            var bru = Parse(file);
            NoteSeq(bru);
            var name = Value(bru.Get("meta"), "name") is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(file.Path);
            var request = BrunoRequests.Map(bru, ImportPath.Join([.. names, name]), context.NewId(), _report, out var mode);
            if (request is null)
            {
                continue;
            }

            _scripts += BrunoScripts.Count(request.Scripts);
            if (mode == BruAuthMode.Inherit && auth.Kind != AuthKind.None)
            {
                request = request with { Auth = auth };
                _authCopies++;
            }

            var merged = Merge(headers, [.. request.Headers]);
            if (merged.Count > request.Headers.Count)
            {
                _headerCopies++;
            }

            items.Add(new ImportRequest(request with { Headers = merged }));
        }

        return items;
    }

    // A folder or collection whose auth is missing, none or inherit is transparent: the inherited auth passes through.
    private RequestAuth Auth(BruFile? file, string path, RequestAuth inherited)
    {
        var mode = Value(file?.Get("auth"), "mode");
        if (file is null || mode is null)
        {
            return inherited;
        }

        var mapped = BrunoAuth.Map(file, mode, path, _report, out var result);
        return result == BruAuthMode.Mapped ? mapped : inherited;
    }

    private sealed class Dir(string name, bool isRoot = false)
    {
        public string Name { get; } = name;

        public ImportFile? FolderFile { get; private set; }

        public List<Dir> Subs { get; } = [];

        public List<ImportFile> Requests { get; } = [];

        public void Add(ReadOnlySpan<string> parts, ImportFile file)
        {
            if (parts.Length == 1)
            {
                if (parts[0] == (isRoot ? CollectionFileName : FolderFileName))
                {
                    FolderFile = file;
                }
                else if (parts[0] != FolderFileName && parts[0] != CollectionFileName)
                {
                    Requests.Add(file);
                }

                return;
            }

            var head = parts[0];
            var sub = Subs.Find(s => s.Name == head);
            if (sub is null)
            {
                sub = new Dir(parts[0]);
                Subs.Add(sub);
            }

            sub.Add(parts[1..], file);
        }
    }
}
