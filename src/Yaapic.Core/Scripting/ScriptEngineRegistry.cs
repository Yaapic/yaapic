using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>Holds one engine per language.</summary>
public sealed class ScriptEngineRegistry
{
    private readonly Dictionary<ScriptLanguage, IScriptEngine> _engines = [];

    /// <summary>Registers an engine; a second engine for the same language replaces the first.</summary>
    /// <param name="engine">The engine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public void Add(IScriptEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engines[engine.Language] = engine;
    }

    /// <summary>Gets the engine of a language.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The registered engine, or one that reports every run as unavailable.</returns>
    public IScriptEngine Get(ScriptLanguage language) =>
        _engines.TryGetValue(language, out var engine) ? engine : new UnavailableScriptEngine(language);

    /// <summary>Tells whether scripts of a language can run.</summary>
    /// <param name="language">The language.</param>
    /// <param name="reason">When unavailable, why; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an available engine is registered.</returns>
    public bool IsAvailable(ScriptLanguage language, out string? reason) => Get(language).IsAvailable(out reason);
}

internal sealed class UnavailableScriptEngine(ScriptLanguage language) : IScriptEngine
{
    private readonly string _reason = $"No {language} engine is installed";

    public ScriptLanguage Language => language;

    public bool IsAvailable(out string? reason)
    {
        reason = _reason;
        return false;
    }

    public Task<ScriptRunResult> RunAsync(ScriptInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return Task.FromResult(ScriptRunResult.Unavailable(invocation.Phase, invocation.Origin, language, _reason));
    }
}
