using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Yaapic.App.WinUI.Services;

/// <summary>Keeps the command buttons of a <see cref="ContentDialog"/> at the Yaapic control metrics.</summary>
internal static class DialogButtons
{
    private static readonly string[] Names = ["PrimaryButton", "SecondaryButton", "CloseButton"];

    /// <summary>Sets the height, weight and alignment of the dialog buttons once its template is applied.</summary>
    /// <param name="dialog">The dialog to normalize.</param>
    /// <remarks>
    /// The dialog template swaps the default button to the stock accent style until focus moves, which drops the style
    /// setters; local values survive that swap.
    /// </remarks>
    /// <param name="inset">
    /// For a dialog whose padding is zero: the margin around the buttons, which then also shrink to their content
    /// and align right instead of filling their column.
    /// </param>
    public static void Normalize(ContentDialog dialog, Thickness? inset = null) => dialog.Loaded += (_, _) => Apply(dialog, inset);

    private static void Apply(DependencyObject parent, Thickness? inset)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button { Name: var name } button && Array.IndexOf(Names, name) >= 0)
            {
                button.MinHeight = (double)Application.Current.Resources["YaapicControlHeight"];
                button.VerticalAlignment = VerticalAlignment.Center;
                button.FontWeight = FontWeights.SemiBold;
                button.CornerRadius = (CornerRadius)Application.Current.Resources["YaapicRadiusControl"];
                if (inset is { } margin)
                {
                    button.Margin = margin;
                    button.HorizontalAlignment = HorizontalAlignment.Right;
                    button.MinWidth = 96;
                }
            }
            else
            {
                Apply(child, inset);
            }
        }
    }
}
