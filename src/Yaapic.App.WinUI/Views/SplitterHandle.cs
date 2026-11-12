using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Yaapic.App.WinUI.Views;

/// <summary>A focusable control that shows a resize cursor, used as a splitter handle (north-south by default, east-west when vertical).</summary>
public sealed partial class SplitterHandle : ContentControl
{
    /// <summary>Initializes the handle with the resize cursor.</summary>
    public SplitterHandle() => SetVertical(false);

    /// <summary>Chooses the resize cursor of a vertical (side by side) or horizontal (stacked) splitter.</summary>
    /// <param name="vertical"><see langword="true"/> for a vertical bar that moves left and right.</param>
    public void SetVertical(bool vertical) =>
        ProtectedCursor = InputSystemCursor.Create(vertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
}
