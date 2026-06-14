using Avalonia.Media.Imaging;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ProfileSectionViewModel : ViewModelBase
{
    private const long MaxAvatarBytes = 8 * 1024 * 1024;

    private readonly IFilePickerService _filePicker;
    private readonly IProfileService _profileService;

    public ProfileSectionViewModel(IFilePickerService filePicker, IProfileService profileService)
    {
        _filePicker = filePicker;
        _profileService = profileService;
    }

    public async Task ActivateAsync()
    {
        try
        {
            var me = await _profileService.GetMeAsync();
            if (me is null) { StatusMessage = "Couldn't load your profile."; return; }

            Username = me.Username;
            Email = me.Email;
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(PasswordHint))]
    private string _oldPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(PasswordHint))]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(HasAvatarChange))]
    private string? _newAvatarPath;

    [ObservableProperty]
    private Bitmap? _avatarPreview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    public bool HasAvatarChange => NewAvatarPath != null;

    // Mirrors UserService.PatchProfile: a new password demands the old one.
    public string? PasswordHint
    {
        get
        {
            if (NewPassword.Length == 0) return null;
            if (NewPassword.Length < 8) return "New password needs at least 8 characters.";
            if (OldPassword.Length == 0) return "Provide the old password to prove you're not an impostor in a very good wig.";
            return null;
        }
    }

    private bool PasswordChangeValid =>
        NewPassword.Length == 0 || (NewPassword.Length >= 8 && OldPassword.Length > 0);

    private bool HasAnyChange =>
        NewAvatarPath != null ||
        NewPassword.Length > 0 ||
        !string.IsNullOrWhiteSpace(Username) ||
        !string.IsNullOrWhiteSpace(Email);

    private bool CanSave => !IsBusy && HasAnyChange && PasswordChangeValid;

    [RelayCommand]
    private async Task PickAvatarAsync()
    {
        StatusMessage = null;
        var path = await _filePicker.PickImageAsync("Choose a new portrait");
        if (path is null) return;

        if (new FileInfo(path).Length > MaxAvatarBytes)
        {
            StatusMessage = "Portrait must be under 8 MB.";
            return;
        }

        try
        {
            var preview = new Bitmap(path);
            AvatarPreview?.Dispose();
            AvatarPreview = preview;
            NewAvatarPath = path;
        }
        catch (Exception)
        {
            StatusMessage = "That file refuses to be an image.";
        }
    }

    [RelayCommand]
    private void ClearAvatar()
    {
        NewAvatarPath = null;
        AvatarPreview?.Dispose();
        AvatarPreview = null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var update = new ProfileUpdate(Username, Email, OldPassword, NewPassword, NewAvatarPath);
            var (ok, error, _) = await _profileService.UpdateAsync(update);
            if (ok)
            {
                OldPassword = string.Empty;
                NewPassword = string.Empty;
                ClearAvatar();
                StatusMessage = "Records updated. The eunuchs have been notified.";
            }
            else StatusMessage = error ?? "Could not update profile.";
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
        finally { IsBusy = false; }
    }
}