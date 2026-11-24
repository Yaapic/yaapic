using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Runner;

/// <summary>Parses a JSON array of objects into iteration data rows.</summary>
public static class JsonIterationData
{
    /// <summary>Parses decoded JSON text; the root must be an array of objects.</summary>
    /// <param name="text">The text, already decoded (no byte order mark).</param>
    /// <returns>One row per array element, values as text (nested values as compact JSON).</returns>
    /// <exception cref="IterationDataException">The JSON is invalid, the root is not an array, an element is not an object, or a limit is exceeded.</exception>
    public static IReadOnlyList<IterationDataRow> Parse(string text)
    {
        DataNode root;
        try
        {
            root = DataNodeJson.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new IterationDataException(Strings.RunnerDataInvalidJson(ex.Message));
        }

        if (!root.IsArray)
        {
            throw new IterationDataException(Strings.RunnerDataJsonNotArray);
        }

        if (root.Items.Count > IterationDataLimits.MaxRows)
        {
            throw new IterationDataException(Strings.RunnerDataTooLarge);
        }

        List<IterationDataRow> rows = new(root.Items.Count);
        for (int i = 0; i < root.Items.Count; i++)
        {
            DataNode item = root.Items[i];
            if (!item.IsObject)
            {
                throw new IterationDataException(Strings.RunnerDataJsonNotObject(i));
            }

            if (item.Members.Count > IterationDataLimits.MaxColumns)
            {
                throw new IterationDataException(Strings.RunnerDataTooLarge);
            }

            rows.Add(new IterationDataRow([.. item.Members.Select(m => new KeyValueEntry(m.Key, ValueText(m.Value)))]));
        }

        return rows;
    }

    private static string ValueText(DataNode node) =>
        node.IsNull ? "" : node.AsString() ?? DataNodeJson.Write(node, indented: false);
}
