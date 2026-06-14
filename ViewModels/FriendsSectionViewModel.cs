using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class FriendsSectionViewModel : ViewModelBase, IActivatable
{
    public async Task ActivateAsync()
    {
        try { await LoadAsync(); }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    private async Task LoadAsync()
    {
        StatusMessage = null;

        var friends = await _service.GetFriendsAsync();
        Friends.Clear();
        foreach (var f in friends)
            Friends.Add(new FriendItem
            {
                RelationshipId = f.Id,
                UserId = f.Friend.Id,
                Username = f.Friend.Username,
                ProfilePictureUrl = f.Friend.ProfilePictureUrl,
                Since = f.CreatedAt,
            });

        var received = await _service.GetReceivedAsync();
        ReceivedRequests.Clear();
        foreach (var r in received?.Relationships ?? [])
            ReceivedRequests.Add(new FriendRequestItem
            {
                RelationshipId = r.Id,
                Username = r.Sender?.Username ?? "?",
                ProfilePictureUrl = r.Sender?.ProfilePictureUrl,
            });

        var sent = await _service.GetSentAsync();
        SentRequests.Clear();
        foreach (var r in sent?.Relationships ?? [])
            SentRequests.Add(new FriendRequestItem
            {
                RelationshipId = r.Id,
                Username = r.Receiver?.Username ?? "?",
                ProfilePictureUrl = r.Receiver?.ProfilePictureUrl,
            });
    }

    public ObservableCollection<FriendItem> Friends { get; } = [];
    public ObservableCollection<FriendRequestItem> ReceivedRequests { get; } = [];
    public ObservableCollection<FriendRequestItem> SentRequests { get; } = [];
    private readonly IFriendService _service;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchUsersCommand))]
    private string _searchQuery = string.Empty;

    public FriendsSectionViewModel(IFriendService service) 
    {
        _service = service;
    }

    private bool CanSearch => !string.IsNullOrWhiteSpace(SearchQuery);
    public ObservableCollection<UserSearchResult> SearchResults { get; } = [];

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchUsersAsync()
    {
        StatusMessage = null;
        SearchResults.Clear();

        var result = await _service.SearchUserAsync(SearchQuery);
        if (result?.Users is null)
        {
            StatusMessage = "Search failed — is the server awake?";
            return;
        }

        foreach (var u in result.Users)
            SearchResults.Add(new UserSearchResult { UserId = u.Id, Username = u.Username });

        StatusMessage = result.Users.Count == 0
            ? $"No one in the registry matches \"{SearchQuery}\"."
            : $"Found {result.Users.Count}.";
    }

    [RelayCommand]
    private async Task SendRequestAsync(UserSearchResult user)
    {
        if (await _service.SendAsync(user.UserId.ToString()))
        {
            StatusMessage = $"Request sent to {user.Username}.";
            SearchResults.Remove(user);
        }
        else StatusMessage = "Could not send request — maybe one already exists.";
    }

    [RelayCommand]
    private async Task AcceptAsync(FriendRequestItem request)
    {
        if (await _service.AcceptAsync(request.RelationshipId))
        {
            ReceivedRequests.Remove(request);
            StatusMessage = $"{request.Username} is now an ally.";
        }
        else StatusMessage = "Could not accept — try again.";
    }

    [RelayCommand]
    private async Task RejectAsync(FriendRequestItem request)
    {
        if (await _service.RejectAsync(request.RelationshipId))
        {
            ReceivedRequests.Remove(request);
            StatusMessage = $"Request from {request.Username} declined. Politely. Probably.";
        }
        else StatusMessage = "Could not reject — try again.";
    }

    [RelayCommand]
    private async Task CancelSentAsync(FriendRequestItem request)
    {
        if (await _service.DeleteAsync(request.RelationshipId))
        {
            SentRequests.Remove(request);
            StatusMessage = "Request withdrawn. It never happened.";
        }
        else StatusMessage = "Could not cancel — try again.";   
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(FriendItem friend)
    {
        if (await _service.DeleteAsync(friend.RelationshipId))
        {
            Friends.Remove(friend);
            StatusMessage = $"{friend.Username} removed. No poison was involved. This time.";
        }
        else StatusMessage = "Could not delete — try again.";
    }
}