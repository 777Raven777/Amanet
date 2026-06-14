using Avalonia.Media.Imaging;
using AvaloniaApplication1.Payloads;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    // Mirror of UserController.ValidateImage (8 MB) and RegisterRequest MinLength(8).
    private const long MaxAvatarBytes = 8 * 1024 * 1024;
    private const int MinPasswordLength = 8;

    private readonly INavigationService _navigation;
    private readonly IFilePickerService _filePicker;
    private readonly IAuthService _auth;

    public RegisterViewModel(INavigationService navigation, IFilePickerService filePicker, IAuthService auth)
    {
        _navigation = navigation;
        _filePicker = filePicker;
        _auth = auth;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    [NotifyPropertyChangedFor(nameof(PasswordHint))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    [NotifyPropertyChangedFor(nameof(PasswordHint))]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    private string? _avatarPath;

    [ObservableProperty]
    private Bitmap? _avatarPreview;

    public bool HasAvatar => AvatarPath != null;

    /// <summary>
    /// Tells the user *why* the Register button is still asleep,
    /// instead of letting them stare at a grey rectangle.
    /// </summary>
    public string? PasswordHint
    {
        get
        {
            if (Password.Length == 0) return null;
            if (Password.Length < MinPasswordLength)
                return $"At least {MinPasswordLength} characters — anything shorter is a diluted antidote.";
            if (ConfirmPassword.Length > 0 && Password != ConfirmPassword)
                return "Passwords do not match.";
            return null;
        }
    }

    private bool CanRegister =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Username) &&
        Password.Length >= MinPasswordLength &&
        Password == ConfirmPassword;

    [RelayCommand]
    private async Task PickAvatarAsync()
    {
        ErrorMessage = null;
        var path = await _filePicker.PickImageAsync("Choose a portrait");
        if (path is null) return;

        // Same gate the backend applies — fail here, not after the upload.
        if (new FileInfo(path).Length > MaxAvatarBytes)
        {
            ErrorMessage = "Portrait must be under 8 MB.";
            return;
        }

        try
        {
            var preview = new Bitmap(path);
            AvatarPreview?.Dispose();
            AvatarPreview = preview;
            AvatarPath = path;
        }
        catch (Exception)
        {
            ErrorMessage = "That file refuses to be an image.";
        }
    }

    [RelayCommand]
    private void ClearAvatar()
    {
        AvatarPath = null;
        AvatarPreview?.Dispose();
        AvatarPreview = null;
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var payload = new RegisterPayload(Email, Username, Password, AvatarPath);
            var result = await _auth.RegisterAsync(payload);
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
    private void GoToLogin() => _navigation.NavigateTo<LoginViewModel>();
}