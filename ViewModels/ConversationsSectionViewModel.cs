using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ConversationsSectionViewModel : ViewModelBase, IActivatable
{
    private readonly IConversationService _conversations;
    private readonly IFriendService _friends;

    public ConversationsSectionViewModel(IConversationService conversations, IFriendService friends)
    {
        _conversations = conversations;
        _friends = friends;
    }

    public ObservableCollection<ConversationListItemDTO> Conversations { get; } = [];
    public ObservableCollection<MessageDTO> Messages { get; } = [];

    // Candidates for a brand-new letter.
    public ObservableCollection<RecipientCandidate> Friends { get; } = [];
    public ObservableCollection<RecipientCandidate> SearchResults { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveConversation))]
    [NotifyPropertyChangedFor(nameof(ThreadTitle))]
    [NotifyPropertyChangedFor(nameof(ShowPicker))]
    [NotifyPropertyChangedFor(nameof(ShowThread))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private ConversationListItemDTO? _selectedConversation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadTitle))]
    [NotifyPropertyChangedFor(nameof(ShowPicker))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private bool _isComposingNew;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadTitle))]
    [NotifyPropertyChangedFor(nameof(ShowPicker))]
    [NotifyPropertyChangedFor(nameof(ShowThread))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private RecipientCandidate? _pendingRecipient;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchRecipientsCommand))]
    private string _recipientQuery = string.Empty;

    private Guid? _cursor;
    private bool _hasMore;

    public bool HasActiveConversation => SelectedConversation != null;

    public string ThreadTitle =>
        SelectedConversation?.OtherUser?.Username
        ?? PendingRecipient?.Username
        ?? (IsComposingNew ? "A new letter" : string.Empty);

    // Right-pane state, kept mutually exclusive.
    public bool ShowPicker => IsComposingNew && PendingRecipient is null;
    public bool ShowThread => SelectedConversation is not null || PendingRecipient is not null;
    public bool ShowEmpty => !IsComposingNew && SelectedConversation is null && PendingRecipient is null;

    public async Task ActivateAsync()
    {
        try
        {
            var list = await _conversations.GetConversationsAsync();
            Conversations.Clear();
            foreach (var c in list?.Conversations ?? [])
                Conversations.Add(c);
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    partial void OnSelectedConversationChanged(ConversationListItemDTO? value)
    {
        if (value is null) return;

        // Choosing an existing thread cancels any half-written new letter.
        IsComposingNew = false;
        PendingRecipient = null;

        _cursor = null;
        _hasMore = false;
        Messages.Clear();
        _ = LoadMessagesAsync(value.Id, initial: true);
    }

    private async Task LoadMessagesAsync(Guid conversationId, bool initial)
    {
        try
        {
            var page = await _conversations.GetMessagesAsync(conversationId, initial ? null : _cursor);
            if (page is null) { StatusMessage = "Couldn't load messages."; return; }

            // Server returns newest-first; insert at 0 so the collection reads oldest→newest top-to-bottom.
            foreach (var m in page.Messages)
                Messages.Insert(0, m);

            _cursor = page.Next;
            _hasMore = page.HasMore;
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    [RelayCommand]
    private async Task LoadOlderAsync()
    {
        if (SelectedConversation is { } c && _hasMore)
            await LoadMessagesAsync(c.Id, initial: false);
    }

    // ----- new letter flow -----

    [RelayCommand]
    private async Task StartNewLetterAsync()
    {
        SelectedConversation = null;
        PendingRecipient = null;
        Messages.Clear();
        SearchResults.Clear();
        RecipientQuery = string.Empty;
        IsComposingNew = true;

        try
        {
            var friends = await _friends.GetFriendsAsync();
            Friends.Clear();
            foreach (var f in friends)
                Friends.Add(new RecipientCandidate
                {
                    Id = f.Friend.Id,
                    Username = f.Friend.Username,
                });
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    [RelayCommand]
    private void CancelNewLetter()
    {
        IsComposingNew = false;
        PendingRecipient = null;
        SearchResults.Clear();
        RecipientQuery = string.Empty;
    }

    private bool CanSearchRecipients => !string.IsNullOrWhiteSpace(RecipientQuery);

    [RelayCommand(CanExecute = nameof(CanSearchRecipients))]
    private async Task SearchRecipientsAsync()
    {
        StatusMessage = null;
        SearchResults.Clear();

        var result = await _friends.SearchUserAsync(RecipientQuery.Trim());
        if (result?.Users is null)
        {
            StatusMessage = "Search failed — is the server awake?";
            return;
        }

        foreach (var u in result.Users)
            SearchResults.Add(new RecipientCandidate { Id = u.Id, Username = u.Username });

        if (result.Users.Count == 0)
            StatusMessage = $"No one in the registry matches \"{RecipientQuery}\".";
    }

    [RelayCommand]
    private void PickRecipient(RecipientCandidate candidate) => PendingRecipient = candidate;

    // ----- sending -----

    private bool CanSend =>
        !string.IsNullOrWhiteSpace(Draft)
        && (SelectedConversation is not null || PendingRecipient is not null);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0) return;

        try
        {
            if (SelectedConversation is { } convo)
            {
                var msg = await _conversations.SendToConversationAsync(convo.Id, text);
                if (msg is null) { StatusMessage = "The letter wouldn't send."; return; }

                Messages.Add(msg);                 // newest goes to the bottom
                Draft = string.Empty;
            }
            else if (PendingRecipient is { } rec)
            {
                var msg = await _conversations.SendToUserAsync(rec.Id, text);
                if (msg is null) { StatusMessage = "The letter wouldn't send."; return; }

                Draft = string.Empty;
                IsComposingNew = false;
                PendingRecipient = null;

                // The response carries no conversation id, so refresh the list and
                // open the thread with this user to continue it.
                await ReloadAndSelectByUserAsync(rec.Id);
            }
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    private async Task ReloadAndSelectByUserAsync(Guid userId)
    {
        var list = await _conversations.GetConversationsAsync();
        Conversations.Clear();
        foreach (var c in list?.Conversations ?? [])
            Conversations.Add(c);

        var match = Conversations.FirstOrDefault(c => c.OtherUser?.Id == userId);
        if (match is not null)
            SelectedConversation = match; // triggers message load
    }
}

public partial class RecipientCandidate : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }

    public string Initial =>
        string.IsNullOrEmpty(Username) ? "?" : Username.Substring(0, 1).ToUpperInvariant();
}