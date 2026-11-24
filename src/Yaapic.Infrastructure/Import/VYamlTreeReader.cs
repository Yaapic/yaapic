using System.Text;
using System.Text.RegularExpressions;
using VYaml;
using VYaml.Parser;
using Yaapic.Core.Import;
using Yaapic.Localization;

namespace Yaapic.Infrastructure.Import;

/// <summary>Reads YAML into a <see cref="DataNode"/> tree with VYaml's low-level parser (no reflection, Native AOT safe).</summary>
public sealed partial class VYamlTreeReader : IYamlTreeReader
{
    private const int MaxDepth = 64;
    private const int MaxNodes = 1_000_000;

    /// <inheritdoc />
    public DataNode Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        try
        {
            var parser = YamlParser.FromBytes(Encoding.UTF8.GetBytes(yaml.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal)));
            parser.SkipHeader();
            if (parser.CurrentEventType == ParseEventType.StreamEnd)
            {
                return DataNode.Null;
            }

            var state = new State();
            return ReadNode(ref parser, state, 0).Node;
        }
        catch (YamlException ex)
        {
            throw new FormatException(Strings.ImportInvalidYaml(ex.Message), ex);
        }
    }

    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    private static FormatException Fail(string reason) => new(Strings.ImportInvalidYaml(reason));

    private static void Count(State state, int nodes)
    {
        state.Count += nodes;
        if (state.Count > MaxNodes)
        {
            throw Fail("expands to too many nodes");
        }
    }

    private static (DataNode Node, bool IsMergeKey) ReadNode(ref YamlParser p, State state, int depth)
    {
        if (depth > MaxDepth)
        {
            throw Fail("nested too deeply");
        }

        var before = state.Count;
        Count(state, 1);
        var anchor = p.TryGetCurrentAnchor(out var a) ? a.Name : null;
        if (p.CurrentEventType == ParseEventType.Alias)
        {
            if (anchor is null || !state.Anchors.TryGetValue(anchor, out var target))
            {
                throw Fail($"unknown alias '{anchor}'");
            }

            // DataNode is immutable, so sharing the anchored subtree is a copy; only its size is charged.
            Count(state, target.Size - 1);
            p.Read();
            return (target.Node, false);
        }

        DataNode node;
        var isMergeKey = false;
        switch (p.CurrentEventType)
        {
            case ParseEventType.Scalar:
                (node, isMergeKey) = ReadScalar(ref p);
                p.Read();
                break;
            case ParseEventType.SequenceStart:
                p.Read();
                var items = new List<DataNode>();
                while (p.CurrentEventType != ParseEventType.SequenceEnd)
                {
                    items.Add(ReadNode(ref p, state, depth + 1).Node);
                }

                p.Read();
                node = DataNode.Array(items);
                break;
            case ParseEventType.MappingStart:
                p.Read();
                node = ReadMapping(ref p, state, depth);
                break;
            default:
                throw Fail($"unexpected {p.CurrentEventType}");
        }

        if (anchor is not null)
        {
            state.Anchors[anchor] = (node, state.Count - before);
        }

        return (node, isMergeKey);
    }

    private static DataNode ReadMapping(ref YamlParser p, State state, int depth)
    {
        var members = new List<KeyValuePair<string, DataNode>>();
        var merged = new List<DataNode>();
        while (p.CurrentEventType != ParseEventType.MappingEnd)
        {
            var (key, isMerge) = ReadNode(ref p, state, depth + 1);
            if (key.Kind is DataKind.Object or DataKind.Array)
            {
                throw Fail("mapping keys must be scalars");
            }

            var value = ReadNode(ref p, state, depth + 1).Node;
            if (isMerge)
            {
                if (value.IsObject)
                {
                    merged.Add(value);
                }
                else if (value.IsArray)
                {
                    merged.AddRange(value.Items.Where(i => i.IsObject));
                }
            }
            else
            {
                members.Add(new(key.AsString() ?? "null", value));
            }
        }

        p.Read();
        var seen = members.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var member in merged.SelectMany(m => m.Members))
        {
            if (seen.Add(member.Key))
            {
                members.Add(member);
            }
        }

        return DataNode.Object(members);
    }

    private static (DataNode Node, bool IsMergeKey) ReadScalar(ref YamlParser p)
    {
        var text = p.GetScalarAsString() ?? "";

        // VYaml exposes no scalar style, but these probes only succeed on plain scalars (see docs/spikes/m5-yaml-tree.md).
        if (p.IsNullScalar())
        {
            return (DataNode.Null, false);
        }

        if (p.TryGetScalarAsBool(out var b) && text is "true" or "false" or "True" or "False" or "TRUE" or "FALSE")
        {
            return (DataNode.Bool(b), false);
        }

        if ((p.TryGetScalarAsInt64(out _) || p.TryGetScalarAsDouble(out _)) && NumberPattern().IsMatch(text))
        {
            return (DataNode.Number(text), false);
        }

        return (DataNode.String(text), text == "<<");
    }

    private sealed class State
    {
        public int Count;

        public Dictionary<string, (DataNode Node, int Size)> Anchors { get; } = [];
    }
}
