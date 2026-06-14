using System;
using AvaloniaApplication1.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.Services;

public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly Func<Type, ViewModelBase> _resolve;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    // Takes a resolver delegate, not the container itself — keeps this
    // class decoupled from DI (depends on an abstraction, not IServiceProvider).
    public NavigationService(Func<Type, ViewModelBase> resolve) => _resolve = resolve;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        => CurrentViewModel = _resolve(typeof(TViewModel));
}

