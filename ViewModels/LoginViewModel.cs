using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IAuthService _auth;

    public LoginViewModel(INavigationService navigation, IAuthService auth)
    {
        _navigation = navigation;
        _auth = auth;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _emailOrUsername = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    private bool CanLogin =>
        !IsBusy && !string.IsNullOrWhiteSpace(EmailOrUsername) && !string.IsNullOrWhiteSpace(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(EmailOrUsername, Password);
            if (result.Success)
                _navigation.NavigateTo<MainViewModel>();
            else
                ErrorMessage = result.Error;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Can't reach the apothecary — is the server awake?";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void GoToRegister() => _navigation.NavigateTo<RegisterViewModel>();
}