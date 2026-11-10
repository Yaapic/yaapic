using Yaapic.Domain.Scripting;

namespace Yaapic.App.Avalonia.Presentation;

public static partial class EditorMap
{
    /// <summary>Gets the code editor language of a script (twin of the WinUI <c>TextMap.ScriptLanguage</c>).</summary>
    /// <param name="language">The script language.</param>
    /// <returns><c>javascript</c> or <c>lua</c>.</returns>
    public static string ScriptLanguage(ScriptLanguage language) => language switch
    {
        Domain.Scripting.ScriptLanguage.Lua => "lua",
        _ => "javascript",
    };

    /// <summary>Gets the index of a phase in the Pre-request / Post-response switch.</summary>
    /// <param name="phase">The script phase.</param>
    /// <returns>0 for pre-request, 1 for post-response.</returns>
    public static int ScriptPhaseIndex(ScriptPhase phase) => phase == ScriptPhase.PostResponse ? 1 : 0;

    /// <summary>Gets the index of a language in the picker (the order of <c>ScriptBlockEditorViewModel.Languages</c>).</summary>
    /// <param name="language">The script language.</param>
    /// <returns>0 for JavaScript, 1 for Lua.</returns>
    public static int ScriptLanguageIndex(ScriptLanguage language) => language == Domain.Scripting.ScriptLanguage.Lua ? 1 : 0;
}
