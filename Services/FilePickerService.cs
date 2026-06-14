using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace AvaloniaApplication1.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> PickImageAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                // Mirrors UserController.ValidateImage on the backend: jpg/jpeg/png only.
                new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}