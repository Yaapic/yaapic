using LoomEditor.Core.Languages.Features;
using LoomEditor.Core.Model;
using LoomEditor.Core.Text;
using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Completes the <c>pm</c> and <c>console</c> API in a script editor, from <see cref="ScriptApiCatalogue"/>.</summary>
/// <param name="lua"><see langword="true"/> to offer the Lua names.</param>
internal sealed class ScriptCompletionProvider(bool lua) : ICompletionItemProvider
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
        if (members.Count == 0)
        {
            return Task.FromResult<CompletionList?>(null);
        }

        var items = new List<CompletionItem>(members.Count);
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
}
