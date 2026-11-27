using Avalonia.Controls;
using Avalonia.Media;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Shows the automation tool windows (runner, load test, mock servers, monitors) as non-modal windows, once each.</summary>
public static class ToolWindowHost
{
    private static readonly List<Window> Open = [];

    /// <summary>Gets or sets the tool window that currently has the focus; while set, dialogs and pickers belong to it.</summary>
    internal static Window? ActiveWindow { get; set; }

    /// <summary>Opens the window held by <paramref name="slot"/>, or activates it when it is already open.</summary>
    /// <param name="slot">The caller's reference to its window; a closed window is replaced on the next call.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Creates the window content, called only when a window is created.</param>
    /// <param name="owner">The main window.</param>
    public static void ShowOrActivate(ref Window? slot, string title, Func<Control> content, Window owner)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(owner);
        // A ref parameter cannot be captured by the Closed handler, so a closed window is detected through Open instead.
        if (slot is not null && Open.Contains(slot))
        {
            slot.Activate();
            return;
        }

        var window = new Window
        {
            Title = title,
            Width = 1100,
            Height = 720,
            MinWidth = 800,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Classes = { "dialog" },
            CanResize = true,
            SizeToContent = SizeToContent.Manual,
            ShowInTaskbar = true,
            Content = content(),
        };
        window.Activated += (_, _) => ActiveWindow = window;
        window.Deactivated += (_, _) => Clear(window);
        window.Closed += (_, _) =>
        {
            Clear(window);
            Open.Remove(window);
        };
        Open.Add(window);
        slot = window;
        window.Show(owner);
    }

    /// <summary>Closes every open tool window; called once the main window close is confirmed.</summary>
    public static void CloseAll()
    {
        foreach (var window in Open.ToArray())
        {
            window.Close();
        }
    }

    private static void Clear(Window window)
    {
        if (ActiveWindow == window)
        {
            ActiveWindow = null;
        }
    }
}
