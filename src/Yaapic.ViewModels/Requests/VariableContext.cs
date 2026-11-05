using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>Tokenises the text of one request tab against its variable scopes and creates the hover popups.</summary>
public sealed class VariableContext
{
    private readonly Guid _requestId;
    private readonly IVariableScopeProvider _scopes;
    private bool _isActive;

    /// <summary>Creates the context.</summary>
    /// <param name="requestId">The request the text belongs to.</param>
    /// <param name="scopes">Provides and writes the variable scopes.</param>
    public VariableContext(Guid requestId, IVariableScopeProvider scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _requestId = requestId;
        _scopes = scopes;
    }

    /// <summary>Raised while <see cref="IsActive"/> when the scopes changed; views tokenise their text again.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the context becomes active, since scopes may have changed while it was not listening.</summary>
    public event EventHandler? Activated;

    /// <summary>Raised when a popup asks to open the editor of a scope.</summary>
    public event EventHandler<VariableScopeKind>? OpenRequested;

    /// <summary>Gets or sets a value indicating whether the tab is active; only an active context listens to scope changes.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            _scopes.ScopesChanged -= OnScopesChanged;
            if (value)
            {
                _scopes.ScopesChanged += OnScopesChanged;
                Activated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Tells whether a name may be created by the popup: a valid placeholder name that is not a dynamic one.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name can be written to a scope.</returns>
    public static bool IsCreatableName(string? name) => VariableScanner.IsValidName(name) && !name!.TrimStart().StartsWith('$');

    /// <summary>Finds the placeholders of a text and how each resolves.</summary>
    /// <param name="text">The text; null or empty yields none.</param>
    /// <returns>The tokens, in order.</returns>
    public IReadOnlyList<VariableToken> Tokenize(string? text)
    {
        if (text is null || !text.Contains("{{", StringComparison.Ordinal))
        {
            return [];
        }

        var spans = VariableScanner.ScanSpans(text);
        if (spans.Count == 0)
        {
            return [];
        }

        var scopes = _scopes.GetScopes(_requestId);
        var tokens = new VariableToken[spans.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = new VariableToken(spans[i].Start, spans[i].Length, Describe(scopes, spans[i].Name));
        }

        return tokens;
    }

    /// <summary>Tells how a variable resolves.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The hint.</returns>
    public VariableHint Describe(string name) => Describe(_scopes.GetScopes(_requestId), name);

    /// <summary>Creates the popup of a variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The popup view model.</returns>
    public VariablePopupViewModel CreatePopup(string name) =>
        new(this, Describe(name), _scopes.GetWritableScopes(_requestId));

    internal Task<string?> SetAsync(VariableScopeKind scope, string name, string value) =>
        _scopes.SetVariableAsync(_requestId, scope, name, value);

    internal void RaiseOpenRequested(VariableScopeKind scope) => OpenRequested?.Invoke(this, scope);

    private static VariableHint Describe(VariableScopes scopes, string name) =>
        scopes.TryResolve(name, out var value, out var scope)
            ? new VariableHint(name, true, scope == VariableScopeKind.Dynamic ? null : value, scope)
            : new VariableHint(name, false, null, null);

    private void OnScopesChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
