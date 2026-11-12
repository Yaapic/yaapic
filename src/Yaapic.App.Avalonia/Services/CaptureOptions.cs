namespace Yaapic.App.Avalonia.Services;

/// <summary>Optional command-line switches used by the screenshot tooling; all of them are inert unless given.</summary>
internal sealed class CaptureOptions
{
    /// <summary>Workspace root replacing the default one, or <see langword="null"/>.</summary>
    public string? Workspace { get; private set; }

    /// <summary>Per-user data directory replacing the real one (vault, update and preference files), or <see langword="null"/>.</summary>
    public string? DataDirectory { get; private set; }

    /// <summary>Initial theme (<c>dark</c>, <c>light</c> or <c>system</c>), or <see langword="null"/>.</summary>
    public string? Theme { get; private set; }

    /// <summary>Scripted update mode (<c>available</c>, <c>downloading</c>, <c>ready</c>, <c>uptodate</c>, <c>failed</c>), or <see langword="null"/> for the real service.</summary>
    public string? UpdateDemo { get; private set; }

    /// <summary>Requested window client size, or <see langword="null"/> to keep the XAML size.</summary>
    public (double Width, double Height)? Size { get; private set; }

    /// <summary>Whether the active tab is sent once the shell is initialised.</summary>
    public bool Send { get; private set; }

    /// <summary>Header text of the request section tab to select, or <see langword="null"/>.</summary>
    public string? Select { get; private set; }

    /// <summary>Name of the window or prompt to open (<c>environments</c>, <c>unsaved</c>, <c>history</c>), or <see langword="null"/>.</summary>
    public string? Open { get; private set; }

    /// <summary>PNG path to write before exiting, or <see langword="null"/> for a normal run.</summary>
    public string? Screenshot { get; private set; }

    /// <summary>Delay in milliseconds between the last action and the capture.</summary>
    public int WaitMs { get; private set; } = 1500;

    /// <summary>Parses the arguments; unknown options and malformed values are ignored.</summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>The parsed options.</returns>
    public static CaptureOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CaptureOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var hasValue = i + 1 < args.Count;
            switch (args[i])
            {
                case "--send":
                    options.Send = true;
                    break;
                case "--workspace" when hasValue:
                    options.Workspace = args[++i];
                    break;
                case "--data-dir" when hasValue:
                    options.DataDirectory = args[++i];
                    break;
                case "--update-demo" when hasValue:
                    options.UpdateDemo = args[++i];
                    break;
                case "--theme" when hasValue:
                    options.Theme = args[++i];
                    break;
                case "--select" when hasValue:
                    options.Select = args[++i];
                    break;
                case "--open" when hasValue:
                    options.Open = args[++i];
                    break;
                case "--screenshot" when hasValue:
                    options.Screenshot = args[++i];
                    break;
                case "--wait-ms" when hasValue:
                    if (int.TryParse(args[++i], out var wait) && wait >= 0)
                    {
                        options.WaitMs = wait;
                    }

                    break;
                case "--size" when hasValue:
                    var parts = args[++i].Split('x');
                    if (parts.Length == 2 && double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h) && w > 0 && h > 0)
                    {
                        options.Size = (w, h);
                    }

                    break;
            }
        }

        return options;
    }
}
