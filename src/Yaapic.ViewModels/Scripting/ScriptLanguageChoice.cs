using Yaapic.Domain.Scripting;

namespace Yaapic.ViewModels.Scripting;

/// <summary>A script language offered by the editor.</summary>
/// <param name="Language">The language.</param>
/// <param name="Label">The label shown to the user.</param>
public sealed record ScriptLanguageChoice(ScriptLanguage Language, string Label);
