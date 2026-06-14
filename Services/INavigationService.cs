using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Services;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
}

