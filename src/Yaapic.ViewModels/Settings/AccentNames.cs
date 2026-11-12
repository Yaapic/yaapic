using Yaapic.Localization;

namespace Yaapic.ViewModels.Settings;

/// <summary>The display name of an accent preset, in the current language.</summary>
public static class AccentNames
{
    /// <summary>Gets the name of a preset.</summary>
    /// <param name="id">A preset id from <c>AccentChoice.Presets</c>.</param>
    /// <returns>The localized name, or the id itself when it is not a preset.</returns>
    public static string Of(string id) => id switch
    {
        "teal" => Strings.AccentTeal,
        "blue" => Strings.AccentBlue,
        "purple" => Strings.AccentPurple,
        "pink" => Strings.AccentPink,
        "red" => Strings.AccentRed,
        "orange" => Strings.AccentOrange,
        "green" => Strings.AccentGreen,
        "grey" => Strings.AccentGrey,
        _ => id,
    };
}
