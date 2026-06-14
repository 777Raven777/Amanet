using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class InvitesSectionViewModel : ViewModelBase, IActivatable
{
    private readonly IServerInvitesService _service;

    public InvitesSectionViewModel(IServerInvitesService service)
    {
        _service = service;
        ReceivedInvites.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasInvites));
    }

    public async Task ActivateAsync()
    {
        try { await LoadAsync(); }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    private async Task LoadAsync()
    {
        StatusMessage = null;

        var received = await _service.ListReceivedServerInvitesAsync();
        ReceivedInvites.Clear();
        foreach (var r in received?.ReceivedInvitesList ?? [])
            ReceivedInvites.Add(new ServerInviteItem { InviteId = r.Id, ServerName = r.ServerName });
    }

    public ObservableCollection<ServerInviteItem> ReceivedInvites { get; } = [];

    public bool HasInvites => ReceivedInvites.Count > 0;

    [RelayCommand]
    private async Task AcceptAsync(ServerInviteItem invite)
    {
        if (await _service.AcceptAsync(invite.InviteId))
        {
            ReceivedInvites.Remove(invite);
            StatusMessage = $"You now serve at {invite.ServerName}. Try the food. Carefully.";
        }
        else StatusMessage = "Could not accept the invite.";
    }

    [RelayCommand]
    private async Task RejectAsync(ServerInviteItem invite)
    {
        if (await _service.RejectAsync(invite.InviteId))
        {
            ReceivedInvites.Remove(invite);
            StatusMessage = $"The summons from {invite.ServerName} was burned. Elegantly.";
        }
        else StatusMessage = "Could not reject the invite.";
    }
}