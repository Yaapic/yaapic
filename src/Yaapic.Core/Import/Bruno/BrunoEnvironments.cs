using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>Maps the <c>environments/*.bru</c> files of a Bruno collection.</summary>
internal static class BrunoEnvironments
{
    private const string Folder = "environments/";
    private const string ProcessEnv = "{{process.env.";

    /// <summary>Maps every environment file below the collection root.</summary>
    /// <param name="input">The imported files.</param>
    /// <param name="rootPath">The path prefix of the collection root (empty or ending with <c>/</c>).</param>
    /// <param name="collectionId">The id of the collection folder the environments are scoped to.</param>
    /// <param name="newId">Produces the identifiers of the environments.</param>
    /// <param name="report">Receives the findings.</param>
    /// <returns>The environments, in input order.</returns>
    internal static IReadOnlyList<EnvironmentDefinition> Map(
        ImportInput input, string rootPath, Guid collectionId, Func<Guid> newId, ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(newId);
        ArgumentNullException.ThrowIfNull(report);

        var prefix = rootPath + Folder;
        List<EnvironmentDefinition> result = [];
        var secrets = 0;
        foreach (var file in input.Files)
        {
            if (!file.Path.StartsWith(prefix, StringComparison.Ordinal) || !BrunoCollection.IsBru(file.Path)
                || file.Path.IndexOf('/', prefix.Length) >= 0)
            {
                continue;
            }

            var bru = BruParser.Parse(file.Text);
            List<KeyValueEntry> rows = [.. (bru.Get("vars")?.Pairs ?? []).Select(p => new KeyValueEntry(p.Key, p.Value, p.Enabled))];
            foreach (var name in bru.Get("vars:secret")?.Items ?? [])
            {
                var index = rows.FindIndex(r => r.Key == name);
                if (index >= 0)
                {
                    rows[index] = rows[index] with { Value = "", IsSecret = true };
                }
                else
                {
                    rows.Add(new KeyValueEntry(name, "", true, true));
                }

                secrets++;
            }

            if (rows.Exists(r => r.Value.Contains(ProcessEnv, StringComparison.Ordinal)))
            {
                report.AddOnce(ImportSeverity.Warning, file.Path, Strings.ImportBrunoProcessEnv);
            }

            result.Add(new EnvironmentDefinition(newId(), file.Path[prefix.Length..^".bru".Length], rows, collectionId));
        }

        if (secrets > 0)
        {
            report.Info("", Strings.ImportBrunoSecretsEmpty(secrets));
        }

        return result;
    }
}
