using Yaapic.Core.Import;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Postman;

/// <summary>Writes the <c>event</c> array of a Postman v2.1 collection, folder or request.</summary>
internal static class PostmanScriptWriter
{
    /// <summary>Writes the scripts of an item.</summary>
    /// <param name="scripts">The scripts.</param>
    /// <param name="path">The report path of the owner.</param>
    /// <param name="report">Receives the warning for a Lua script.</param>
    /// <returns>The <c>event</c> node, or null when nothing remains.</returns>
    internal static DataNode? Events(ScriptSet scripts, string path, ImportReport report)
    {
        List<DataNode> events = [];
        foreach (var (listen, block) in new[] { ("prerequest", scripts.PreRequest), ("test", scripts.PostResponse) })
        {
            if (block.IsEmpty)
            {
                continue;
            }

            if (block.Language != ScriptLanguage.JavaScript)
            {
                report.Warning(path, Strings.ExportPostmanLuaScript(path));
                continue;
            }

            var lines = YaapicToPostmanRewriter.Rewrite(block.Source).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            events.Add(DataNodeJson.Obj(
                ("listen", DataNodeJson.Str(listen)),
                ("script", DataNodeJson.Obj(
                    ("type", DataNodeJson.Str("text/javascript")),
                    ("exec", DataNodeJson.Arr([.. lines.Select(DataNodeJson.Str)]))))));
        }

        return events.Count == 0 ? null : DataNodeJson.Arr([.. events]);
    }
}
