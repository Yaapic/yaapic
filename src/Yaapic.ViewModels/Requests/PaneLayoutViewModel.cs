using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yaapic.ViewModels.Requests;

/// <summary>The request / response panel layout, shared by every open request tab.</summary>
public sealed partial class PaneLayoutViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isHorizontal;

    [RelayCommand]
    private void Toggle() => IsHorizontal = !IsHorizontal;
}
