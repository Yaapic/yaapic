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
}
