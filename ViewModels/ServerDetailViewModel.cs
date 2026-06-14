using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ServerDetailViewModel : ViewModelBase
{
    private readonly IServerService _service;
    private readonly IServerInvitesService _inviteService;

    public Guid ServerId { get; }

    public ObservableCollection<ChannelItem> Channels { get; } = [];
    public ObservableCollection<ParticipantItem> Participants { get; } = [];
    public ObservableCollection<RoleListItem> Roles { get; } = [];
    public ObservableCollection<InviteCandidate> InviteCandidates { get; } = [];
    public ObservableCollection<SentInviteItem> SentInvites { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateChannelCommand))]
    private string _newChannelName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateRoleCommand))]
    private string _newRoleName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCandidatesCommand))]
    private string _inviteQuery = string.Empty;

    public ObservableCollection<PermissionToggle> NewRoleToggles { get; }

    public ServerDetailViewModel(IServerService service, IServerInvitesService inviteService, Guid serverId)
    {
        _service = service;
        _inviteService = inviteService;
        ServerId = serverId;
        NewRoleToggles = PermissionInfo.BuildToggles(null);
    }

    public async Task LoadAsync()
    {
        StatusMessage = null;

        var channels = await _service.GetChannelsAsync(ServerId);
        Channels.Clear();
        foreach (var c in channels)
            Channels.Add(new ChannelItem { Id = c.Id, Name = c.Name });

        var roles = await _service.GetRolesAsync(ServerId);
        Roles.Clear();
        foreach (var r in roles)
            Roles.Add(new RoleListItem { Id = r.Id, IsSystem = r.IsSystem, Name = r.Name });

        var page = await _service.GetParticipantsAsync(ServerId);
        Participants.Clear();
        foreach (var p in page?.Participants ?? [])
            Participants.Add(new ParticipantItem
            {
                Id = p.Id,
                Username = p.Username,
                ProfilePictureUrl = p.ProfilePictureUrl,
                RoleName = p.RoleName,
            });

        var invites = await _inviteService.ListSentServerInvitesAsync(ServerId);
        SentInvites.Clear();
        foreach (var i in invites?.InvitesList ?? [])
            SentInvites.Add(new SentInviteItem { Id = i.Id, InvitedUsername = i.InvitedUsername });
    }

    private bool CanCreateChannel => !string.IsNullOrWhiteSpace(NewChannelName);

    [RelayCommand(CanExecute = nameof(CanCreateChannel))]
    private async Task CreateChannelAsync()
    {
        var name = NewChannelName.Trim();

        var created = await _service.CreateChannelAsync(ServerId, name);
        if (created is null)
        {
            StatusMessage = "Could not open the chamber — try again.";
            return;
        }

        Channels.Add(new ChannelItem { Id = created.Id, Name = created.Name });
        NewChannelName = string.Empty;
        StatusMessage = $"#{created.Name} opened.";
    }

    [RelayCommand]
    private void StartRenameChannel(ChannelItem channel)
    {
        channel.RenameDraft = channel.Name;
        channel.IsRenaming = true;
    }

    [RelayCommand]
    private void CancelRenameChannel(ChannelItem channel) => channel.IsRenaming = false;

    [RelayCommand]
    private async Task ConfirmRenameChannelAsync(ChannelItem channel)
    {
        var name = channel.RenameDraft.Trim();
        if (name.Length == 0)
        {
            StatusMessage = "A chamber needs a name.";
            return;
        }

        if (await _service.EditChannelAsync(ServerId, channel.Id, name))
        {
            channel.Name = name;
            channel.IsRenaming = false;
        }
        else StatusMessage = "Could not rename — try again.";
    }

    [RelayCommand]
    private async Task DeleteChannelAsync(ChannelItem channel)
    {
        if (await _service.DeleteChannelAsync(ServerId, channel.Id))
        {
            Channels.Remove(channel);
            StatusMessage = $"#{channel.Name} sealed.";
        }
        else StatusMessage = "Could not seal — try again.";
    }

    [RelayCommand]
    private void StartChangeRole(ParticipantItem participant)
    {
        foreach (var p in Participants) p.IsChangingRole = false;
        participant.IsChangingRole = true;
    }

    [RelayCommand]
    private void CancelChangeRole(ParticipantItem participant) => participant.IsChangingRole = false;

    [RelayCommand]
    private async Task AssignRoleAsync(RoleListItem role)
    {
        var participant = Participants.FirstOrDefault(p => p.IsChangingRole);
        if (participant is null) return;

        if (await _service.ModifyParticipantAsync(ServerId, participant.Id, role.Id, null))
        {
            participant.RoleName = role.Name;
            participant.IsChangingRole = false;
            StatusMessage = $"{participant.Username} is now {role.Name}.";
        }
        else StatusMessage = "Could not reassign — perhaps you lack the seal.";
    }

    [RelayCommand]
    private async Task RemoveParticipantAsync(ParticipantItem participant)
    {
        if (await _service.RemoveParticipantAsync(ServerId, participant.Id))
        {
            Participants.Remove(participant);
            StatusMessage = $"{participant.Username} removed from the pavilion.";
        }
        else StatusMessage = "Could not remove — perhaps you lack the seal.";
    }

    private bool CanCreateRole => !string.IsNullOrWhiteSpace(NewRoleName);

    [RelayCommand(CanExecute = nameof(CanCreateRole))]
    private async Task CreateRoleAsync()
    {
        var name = NewRoleName.Trim();
        var granted = NewRoleToggles.Where(t => t.IsGranted).Select(t => t.Permission).ToList();

        var created = await _service.CreateRoleAsync(ServerId, name, granted);
        if (created is null)
        {
            StatusMessage = "Could not establish the rank — try again.";
            return;
        }

        Roles.Add(new RoleListItem { Id = created.Id, IsSystem = created.IsSystem, Name = created.Name });
        NewRoleName = string.Empty;
        foreach (var t in NewRoleToggles) t.IsGranted = false;
        StatusMessage = $"Rank \"{created.Name}\" established.";
    }

    [RelayCommand]
    private async Task StartEditRoleAsync(RoleListItem role)
    {
        if (role.IsSystem) return;

        var full = await _service.GetRoleAsync(ServerId, role.Id);
        var granted = full?.Actions;

        role.EditToggles.Clear();
        foreach (var toggle in PermissionInfo.BuildToggles(granted))
            role.EditToggles.Add(toggle);

        role.NameDraft = role.Name;
        role.IsEditing = true;
    }

    [RelayCommand]
    private void CancelEditRole(RoleListItem role) => role.IsEditing = false;

    [RelayCommand]
    private async Task ConfirmEditRoleAsync(RoleListItem role)
    {
        var name = role.NameDraft.Trim();
        if (name.Length == 0)
        {
            StatusMessage = "A rank needs a name.";
            return;
        }

        var granted = role.EditToggles.Where(t => t.IsGranted).Select(t => t.Permission).ToList();

        if (await _service.EditRoleAsync(ServerId, role.Id, name, granted))
        {
            role.Name = name;
            role.IsEditing = false;
            StatusMessage = $"Rank \"{name}\" updated.";
        }
        else StatusMessage = "Could not update the rank — perhaps you lack the seal.";
    }

    [RelayCommand]
    private async Task DeleteRoleAsync(RoleListItem role)
    {
        if (role.IsSystem) return;

        if (await _service.DeleteRoleAsync(ServerId, role.Id))
        {
            Roles.Remove(role);
            StatusMessage = $"Rank \"{role.Name}\" abolished. Its holders fall back to default.";
        }
        else StatusMessage = "Could not abolish — try again.";
    }

    private bool CanSearchCandidates => !string.IsNullOrWhiteSpace(InviteQuery);

    [RelayCommand(CanExecute = nameof(CanSearchCandidates))]
    private async Task SearchCandidatesAsync()
    {
        StatusMessage = null;
        InviteCandidates.Clear();

        var result = await _service.SearchUsersAsync(InviteQuery.Trim());
        if (result?.Users is null)
        {
            StatusMessage = "Search failed — is the server awake?";
            return;
        }

        foreach (var u in result.Users)
            InviteCandidates.Add(new InviteCandidate { UserId = u.Id, Username = u.Username });

        if (result.Users.Count == 0)
            StatusMessage = $"No one in the registry matches \"{InviteQuery}\".";
    }

    [RelayCommand]
    private async Task SendInviteAsync(InviteCandidate candidate)
    {
        var invite = await _inviteService.SendAsync(ServerId, candidate.UserId);
        if (invite is not null)
        {
            InviteCandidates.Remove(candidate);
            SentInvites.Add(new SentInviteItem { Id = invite.Id, InvitedUsername = invite.InvitedUsername });
            StatusMessage = $"Summons sent to {candidate.Username}.";
        }
        else StatusMessage = "Could not send — they may already be a resident or already summoned.";
    }

    [RelayCommand]
    private async Task RescindInviteAsync(SentInviteItem invite)
    {
        if (await _inviteService.DeleteAsync(ServerId, invite.Id))
        {
            SentInvites.Remove(invite);
            StatusMessage = $"Summons to {invite.InvitedUsername} rescinded.";
        }
        else StatusMessage = "Could not rescind — try again.";
    }
}

public partial class ChannelItem : ObservableObject
{
    public required Guid Id { get; init; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameDraft = string.Empty;
}

public partial class ParticipantItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public string? ProfilePictureUrl { get; init; }

    [ObservableProperty] private string _roleName = string.Empty;
    [ObservableProperty] private bool _isChangingRole;

    public string Initial => string.IsNullOrEmpty(Username) ? "?" : Username.Substring(0, 1).ToUpperInvariant();
}

public partial class RoleListItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required bool IsSystem { get; init; }

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowManageButtons))]
    private bool _isEditing;

    [ObservableProperty] private string _nameDraft = string.Empty;

    public ObservableCollection<PermissionToggle> EditToggles { get; } = [];

    public bool ShowManageButtons => !IsSystem && !IsEditing;
}

public partial class PermissionToggle : ObservableObject
{
    public required Permissions Permission { get; init; }
    public required string Label { get; init; }

    [ObservableProperty] private bool _isGranted;
}

public partial class InviteCandidate : ObservableObject
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }

    public string Initial => string.IsNullOrEmpty(Username) ? "?" : Username.Substring(0, 1).ToUpperInvariant();
}

public partial class SentInviteItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string InvitedUsername { get; init; }

    public string Initial => string.IsNullOrEmpty(InvitedUsername) ? "?" : InvitedUsername.Substring(0, 1).ToUpperInvariant();
}

public static class PermissionInfo
{
    public static readonly Permissions[] All =
    [
        Permissions.SendMessages,
        Permissions.ReadMessages,
        Permissions.EditMessages,
        Permissions.DeleteMessages,
        Permissions.InviteUsers,
        Permissions.CreateChannels,
        Permissions.EditUsers,
        Permissions.BanUsers,
        Permissions.ModifyRoles,
    ];

    public static string Label(Permissions p) => p switch
    {
        Permissions.SendMessages => "Send messages",
        Permissions.ReadMessages => "Read messages",
        Permissions.EditMessages => "Edit messages",
        Permissions.DeleteMessages => "Delete messages",
        Permissions.InviteUsers => "Invite users",
        Permissions.CreateChannels => "Create chambers",
        Permissions.EditUsers => "Edit residents",
        Permissions.BanUsers => "Remove residents",
        Permissions.ModifyRoles => "Modify ranks",
        _ => p.ToString(),
    };

    public static ObservableCollection<PermissionToggle> BuildToggles(IEnumerable<Permissions>? granted)
    {
        var set = granted is null ? new HashSet<Permissions>() : new HashSet<Permissions>(granted);
        var list = new ObservableCollection<PermissionToggle>();
        foreach (var p in All)
            list.Add(new PermissionToggle { Permission = p, Label = Label(p), IsGranted = set.Contains(p) });
        return list;
    }
}