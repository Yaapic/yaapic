using System.Text;
using System.Text.Json;
using Yaapic.Composition;
using Yaapic.Core.Scripting;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Scripting;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// A temporary workspace written as YAML files (so the file format is exercised), a vault location outside the real user
/// data, and shells composed with the real QuickJS engine.
/// </summary>
internal sealed class ScriptedShellFixture : IDisposable
{
    private readonly string _vaults = Path.Combine(Path.GetTempPath(), "yaapic-e2e3-vaults-" + Guid.NewGuid().ToString("N"));

    public ScriptedShellFixture() => Root = Path.Combine(Path.GetTempPath(), "yaapic-e2e3-" + Guid.NewGuid().ToString("N"));

    public string Root { get; }

    public void Dispose()
    {
        TempWorkspace.Delete(Root);
        TempWorkspace.Delete(_vaults);
    }

    /// <summary>Gets the path of the vault file of the workspace, in the temporary vault directory.</summary>
    public string VaultFile => new TempVaultLocation(_vaults).GetVaultPath(Workspace.ReadWorkspaceId(Root));

    /// <summary>Gets a path under the workspace root.</summary>
    public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Writes a folder file; <paramref name="folderPath"/> is relative to <c>requests</c>.</summary>
    public void WriteFolder(string folderPath, string name, string? pre = null, string? post = null, string variables = "[]", string preLanguage = "javascript", string postLanguage = "javascript")
    {
        var yaml = $"version: 1\nid: {Guid.NewGuid():D}\nname: {name}\ndescription: \"\"\nvariables: {variables}\n{ScriptsYaml(pre, post, preLanguage, postLanguage)}";
        Write(PathOf("requests", folderPath, "folder.yaml"), yaml);
    }

    /// <summary>Writes a request file and returns its id.</summary>
    public Guid WriteRequest(string folderPath, string name, string url, string? pre = null, string? post = null, (string Key, string Value)? headers = null, string preLanguage = "javascript", string postLanguage = "javascript")
    {
        var id = Guid.NewGuid();
        var headerYaml = headers is not { } h
            ? "headers: []\n"
            : $"headers:\n- key: {Quote(h.Key)}\n  value: {Quote(h.Value)}\n  enabled: true\n";
        var yaml = $"version: 1\nid: {id:D}\nname: {name}\nmethod: GET\nurl: {Quote(url)}\nparams: []\n{headerYaml}{ScriptsYaml(pre, post, preLanguage, postLanguage)}";
        Write(PathOf("requests", folderPath, name.ToLowerInvariant().Replace(' ', '-') + ".request.yaml"), yaml);
        return id;
    }

    /// <summary>Writes an environment file.</summary>
    public void WriteEnvironment(string name, params (string Key, string Value)[] variables)
    {
        var rows = variables.Length == 0
            ? "variables: []\n"
            : "variables:\n" + string.Concat(variables.Select(v => $"- key: {Quote(v.Key)}\n  value: {Quote(v.Value)}\n  enabled: true\n"));
        Write(PathOf("environments", name.ToLowerInvariant() + ".env.yaml"), $"version: 1\nid: {Guid.NewGuid():D}\nname: {name}\n{rows}");
    }

    /// <summary>Composes a shell with the real QuickJS engine and initializes it.</summary>
    public async Task<ShellViewModel> NewShellAsync(ScriptLimits? limits = null, bool withLua = false, IKeyProtectorFactory? protectors = null)
    {
        IScriptEngine[] engines = withLua ? [new QuickJsEngine(), new LuaEngine()] : [new QuickJsEngine()];
        var shell = AppComposition.CreateShell(Root, new FakeDialogService(), protectors, new TempVaultLocation(_vaults), engines, limits);
        await shell.InitializeAsync();
        var unavailable = shell.ScriptEngineWarnings.FirstOrDefault(w => w.StartsWith("JavaScript", StringComparison.Ordinal) || (withLua && w.StartsWith("Lua", StringComparison.Ordinal)));
        Assert.True(
            unavailable is null,
            "A native script library is missing (" + unavailable + "). Run 'pwsh native/Build-Natives.ps1 -Rid <rid>' to build it.");
        return shell;
    }

    /// <summary>Opens a saved request in a tab and returns the tab.</summary>
    public static RequestEditorViewModel Open(ShellViewModel shell, Guid id)
    {
        shell.Collections.OpenRequest(id);
        return shell.Tabs.Tabs.Single(t => t.Id == id);
    }

    /// <summary>Parses the echoed JSON body of a completed response.</summary>
    public static JsonElement Json(RequestEditorViewModel tab)
    {
        Assert.Equal(ResponseState.Completed, tab.Response.State);
        return JsonDocument.Parse(tab.Response.BodyText).RootElement;
    }

    /// <summary>Gets the messages of the console lines of the given kind, in order.</summary>
    public static string[] Messages(ShellViewModel shell, ConsoleLineKind kind) =>
        [.. shell.Console.Lines.Where(l => l.Kind == kind).Select(l => l.Message)];

    private static string ScriptsYaml(string? pre, string? post, string preLanguage, string postLanguage) =>
        $"scripts:\n  preRequest:\n    language: {preLanguage}\n" + Block(pre) + $"  postResponse:\n    language: {postLanguage}\n" + Block(post);

    private static string Block(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "    source: \"\"\n";
        }

        var lines = source.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        return "    source: |\n" + string.Concat(lines.Select(l => "      " + l + "\n"));
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    internal sealed class TempVaultLocation(string directory) : IVaultLocation
    {
        public string GetVaultPath(string workspaceId)
        {
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, workspaceId + ".vault");
        }
    }
}
