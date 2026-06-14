using AvaloniaApplication1.Services;

namespace AvaloniaApplication1.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public INavigationService Navigation { get; }

    public MainWindowViewModel(INavigationService navigation)
    {
        Navigation = navigation;
        Navigation.NavigateTo<LoginViewModel>(); // landing page
    }
}