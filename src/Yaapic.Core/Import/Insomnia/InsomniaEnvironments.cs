using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Import.Insomnia;

/// <summary>Maps Insomnia base environments to collection variables and sub environments to collection environments.</summary>
internal static class InsomniaEnvironments
{
    private const int MaxFlattenDepth = 4;

    /// <summary>Reads every <c>environment</c> resource of the export.</summary>
    /// <param name="resources">The resources of the export, in order.</param>
    /// <param name="workspaceId">The identifier of the imported workspace, if any.</param>
    /// <param name="collectionId">The identifier of the collection folder the environments are scoped to.</param>
    /// <param name="context">The import context.</param>
    /// <param name="path">The report path of the collection.</param>
    /// <returns>The collection variables and the sub environments.</returns>
    internal static (List<KeyValueEntry> Variables, List<EnvironmentDefinition> Environments) Read(
        IReadOnlyList<DataNode> resources, string? workspaceId, Guid collectionId, ImportContext context, string path)
    {
        var all = resources.Where(r => r.GetString("_type") == "environment").ToList();
        var baseIds = all
            .Where(e => e.GetString("parentId") == workspaceId && e.GetString("_id") is not null)
            .Select(e => e.GetString("_id")!)
            .ToHashSet(StringComparer.Ordinal);

        List<KeyValueEntry> variables = [];
        List<EnvironmentDefinition> environments = [];
        foreach (var env in all)
        {
            var rows = Flatten(env["data"], path, context.Report);
            if (env.GetString("_id") is { } id && baseIds.Contains(id))
            {
                foreach (var row in rows)
                {
                    var at = variables.FindIndex(v => v.Key == row.Key);
                    if (at >= 0)
                    {
                        variables[at] = row;
                    }
                    else
                    {
                        variables.Add(row);
                    }
                }
            }
            else
            {
                environments.Add(new EnvironmentDefinition(
                    context.NewId(), ImportHelpers.SafeName(env.GetString("name"), "Environment"), rows, collectionId));
            }
        }

        return (variables, environments);
    }

    private static List<KeyValueEntry> Flatten(DataNode? data, string path, ImportReport report)
    {
        List<KeyValueEntry> rows = [];
        if (data is { IsObject: true })
        {
            Add(rows, data, "", 1, path, report);
        }

        return rows;
    }

    private static void Add(List<KeyValueEntry> rows, DataNode obj, string prefix, int depth, string path, ImportReport report)
    {
        foreach (var (name, value) in obj.Members)
        {
            var key = prefix + name;
            switch (value.Kind)
            {
                case DataKind.Object when depth <= MaxFlattenDepth:
                    Add(rows, value, key + ".", depth + 1, path, report);
                    break;
                case DataKind.Object or DataKind.Array:
                    rows.Add(new KeyValueEntry(key, DataNodeJson.Write(value, false)));
                    break;
                case DataKind.String:
                    rows.Add(new KeyValueEntry(key, InsomniaTemplates.Translate(value.AsString() ?? "", path, report)));
                    break;
                default:
                    rows.Add(new KeyValueEntry(key, value.AsString() ?? ""));
                    break;
            }
        }
    }
}
