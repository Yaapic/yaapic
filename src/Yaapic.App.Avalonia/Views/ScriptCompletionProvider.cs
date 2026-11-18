using LoomEditor.Core.Languages.Features;
using LoomEditor.Core.Model;
using LoomEditor.Core.Text;
using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Completes the <c>yaapic</c> and <c>console</c> API and the snippets in a script editor, from <see cref="ScriptApiCatalogue"/> and <see cref="ScriptSnippetCatalogue"/>.</summary>
/// <param name="lua"><see langword="true"/> to offer the Lua names.</param>
/// <param name="phase">Gives the phase the editor shows, to filter the snippets.</param>
internal sealed class ScriptCompletionProvider(bool lua, Func<ScriptSnippetPhase> phase) : ICompletionItemProvider
{
    /// <inheritdoc/>
    public IReadOnlyList<string>? TriggerCharacters { get; } = new[] { "." };

    /// <inheritdoc/>
    public bool CanResolve => false;

    /// <inheritdoc/>
    public Task<CompletionList?> ProvideCompletionItemsAsync(TextModel model, Position position, CompletionContext context, CancellationToken cancellationToken)
    {
        var line = model.GetLineContent(position.LineNumber);
        var before = line[..Math.Min(Math.Max(position.Column - 1, 0), line.Length)];
        var (_, members) = ScriptApiCatalogue.Complete(before, lua, context.TriggerKind == CompletionTriggerKind.Invoke);
        var snippets = SnippetsFor(before);
        if (members.Count == 0 && snippets.Count == 0)
        {
            return Task.FromResult<CompletionList?>(null);
        }

        var items = new List<CompletionItem>(members.Count + snippets.Count);
        foreach (var snippet in snippets)
        {
            items.Add(new CompletionItem(snippet.Prefix, snippet.Template(lua)!, CompletionItemKind.Snippet)
            {
                Detail = snippet.Name,
                InsertTextRules = InsertTextRules.InsertAsSnippet,
            });
        }

        foreach (var member in members)
        {
            var name = member.NameIn(lua);
            var isMethod = member.Kind == ScriptApiKind.Method;
            var item = new CompletionItem(name, isMethod ? name + (member.Signature == "()" ? "()" : "($0)") : name, isMethod ? CompletionItemKind.Method : CompletionItemKind.Property)
            {
                Detail = member.Description,
                LabelDetail = isMethod ? member.Signature : "",
            };
            if (isMethod && member.Signature != "()")
            {
                item.InsertTextRules = InsertTextRules.InsertAsSnippet;
            }

            items.Add(item);
        }

        return Task.FromResult<CompletionList?>(new CompletionList(items, false));
    }

    // Snippets are offered on a bare word only: after a dot the user is typing a member.
    private List<ScriptSnippet> SnippetsFor(string before)
    {
        var start = before.Length;
        while (start > 0 && (char.IsLetterOrDigit(before[start - 1]) || before[start - 1] == '_'))
        {
            start--;
        }

        if (start > 0 && before[start - 1] == '.')
        {
            return [];
        }

        var word = before[start..];
        return [.. ScriptSnippetCatalogue.For(lua, phase()).Where(s => word.Length > 0 && s.Prefix.StartsWith(word, StringComparison.OrdinalIgnoreCase))];
    }
}
