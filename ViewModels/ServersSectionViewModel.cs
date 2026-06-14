using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ServersSectionViewModel : ViewModelBase, IActivatable
{
    private readonly IServerService _service;
    private readonly IServerInvitesService _inviteService;

    public ObservableCollection<ServerItem> Servers { get; } = [];

    [ObservableProperty]
    private ServerItem? _selectedServer;

    [ObservableProperty]
    private ServerDetailViewModel? _serverDetail;

    [ObservableProperty]
    private bool _isRenamingServer;

    [ObservableProperty]
    private string _serverRenameDraft = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateServerCommand))]
    private string _newServerName = string.Empty;

    public ServersSectionViewModel(IServerService service, IServerInvitesService inviteService)
    {
        _service = service;
        _inviteService = inviteService;
    }

    public async Task ActivateAsync()
    {
        try { await LoadServersAsync(); }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    private async Task LoadServersAsync()
    {
        StatusMessage = null;

        var page = await _service.GetServersAsync();
        Servers.Clear();
        foreach (var s in page?.Servers ?? [])
            Servers.Add(new ServerItem { Id = s.Id, Name = s.Name });
    }

    partial void OnSelectedServerChanged(ServerItem? value)
    {
        IsRenamingServer = false;

        if (value is null)
        {
            ServerDetail = null;
            return;
        }

        var detail = new ServerDetailViewModel(_service, _inviteService, value.Id);
        ServerDetail = detail;
        _ = LoadDetailAsync(detail);
    }

    private static async Task LoadDetailAsync(ServerDetailViewModel detail)
    {
        try { await detail.LoadAsync(); }
        catch (HttpRequestException) { detail.StatusMessage = "Couldn't reach the server."; }
    }

    private bool CanCreateServer => !string.IsNullOrWhiteSpace(NewServerName);

    [RelayCommand(CanExecute = nameof(CanCreateServer))]
    private async Task CreateServerAsync()
    {
        var name = NewServerName.Trim();

        var created = await _service.CreateServerAsync(name);
        if (created is null)
        {
            StatusMessage = "Could not found the pavilion — try again.";
            return;
        }

        var item = new ServerItem { Id = created.Id, Name = created.Name };
        Servers.Add(item);
        NewServerName = string.Empty;
        SelectedServer = item;
        StatusMessage = $"\"{item.Name}\" founded.";
    }

    [RelayCommand]
    private void StartRenameServer()
    {
        if (SelectedServer is null) return;
        ServerRenameDraft = SelectedServer.Name;
        IsRenamingServer = true;
    }

    [RelayCommand]
    private void CancelRenameServer() => IsRenamingServer = false;

    [RelayCommand]
    private async Task ConfirmRenameServerAsync()
    {
        if (SelectedServer is null) return;

        var name = ServerRenameDraft.Trim();
        if (name.Length == 0)
        {
            StatusMessage = "A pavilion needs a name.";
            return;
        }

        if (await _service.EditServerAsync(SelectedServer.Id, name))
        {
            SelectedServer.Name = name;
            IsRenamingServer = false;
        }
        else StatusMessage = "Could not rename — try again.";
    }

    [RelayCommand]
    private async Task DeleteServerAsync()
    {
        if (SelectedServer is null) return;

        var target = SelectedServer;
        if (await _service.DeleteServerAsync(target.Id))
        {
            Servers.Remove(target);
            SelectedServer = null;
            StatusMessage = "Pavilion dissolved. The carpenters wept.";
        }
        else StatusMessage = "Could not dissolve — try again.";
    }
}

public partial class ServerItem : ObservableObject
{
    public required Guid Id { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    private string _name = string.Empty;

    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpperInvariant();
}
