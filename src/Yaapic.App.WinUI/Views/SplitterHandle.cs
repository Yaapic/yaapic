using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Yaapic.App.WinUI.Views;

/// <summary>A focusable control that shows the north-south resize cursor, used as a horizontal splitter handle.</summary>
public sealed partial class SplitterHandle : ContentControl
{
    /// <summary>Initializes the handle with the resize cursor.</summary>
    public SplitterHandle() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
}
