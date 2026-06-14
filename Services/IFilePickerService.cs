using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

/// <summary>
/// Abstraction over the OS file dialog. ViewModels depend on this interface
/// instead of Avalonia's StorageProvider directly (DIP — same reasoning as
/// IFileService on the backend: the VM shouldn't know where files come from).
/// </summary>
public interface IFilePickerService
{
    /// <returns>Local path of the chosen image, or null if cancelled.</returns>
    Task<string?> PickImageAsync(string title);
}