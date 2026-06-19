using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ConversationsSectionViewModel : ViewModelBase,
    IActivatable, IRecipient<PrivateMessageReceived>, IDisposable
{
    private readonly IConversationService _conversations;
    private readonly IFriendService _friends;
    private readonly IChatHub _hub;

    public ConversationsSectionViewModel(IConversationService conversations, IFriendService friends, IChatHub hub)
    {
        _conversations = conversations;
        _friends = friends;
        _hub = hub;
        WeakReferenceMessenger.Default.Register(this);
    }

    public ObservableCollection<ConversationListItemDTO> Conversations { get; } = [];
    public ObservableCollection<MessageDTO> Messages { get; } = [];

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

            await _hub.ConnectAsync();   // idempotent — shared singleton connection
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    partial void OnSelectedConversationChanged(ConversationListItemDTO? value)
    {
        if (value is null) return;

        IsComposingNew = false;
        PendingRecipient = null;

        _cursor = null;
        _hasMore = false;
        Messages.Clear();
        _ = LoadMessagesAsync(value.Id, initial: true);
        _ = _hub.JoinConversationAsync(value.Id);   // join group for live updates
    }

    public void Receive(PrivateMessageReceived message)
    {
        var m = message.Message;

        if (m.SourceId != SelectedConversation?.Id) return;

        if (Messages.Any(x => x.Id == m.Id)) return;   // dedup (our own echo, etc.)
        Messages.Add(m);
    }

    private async Task LoadMessagesAsync(Guid conversationId, bool initial)
    {
        try
        {
            var page = await _conversations.GetMessagesAsync(conversationId, initial ? null : _cursor);
            if (page is null) { StatusMessage = "Couldn't load messages."; return; }

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
                Friends.Add(new RecipientCandidate { Id = f.Friend.Id, Username = f.Friend.Username });
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
                await _hub.SendPrivateMessageAsync(convo.Id, text);
                Draft = string.Empty;
            }
            else if (PendingRecipient is { } rec)
            {
                var msg = await _conversations.SendToUserAsync(rec.Id, text);
                if (msg is null) { StatusMessage = "The letter wouldn't send."; return; }

                Draft = string.Empty;
                IsComposingNew = false;
                PendingRecipient = null;

                await ReloadAndSelectByUserAsync(rec.Id);
            }
        }
        catch (Exception) { StatusMessage = "The letter wouldn't send."; }
    }

    private async Task ReloadAndSelectByUserAsync(Guid userId)
    {
        var list = await _conversations.GetConversationsAsync();
        Conversations.Clear();
        foreach (var c in list?.Conversations ?? [])
            Conversations.Add(c);

        var match = Conversations.FirstOrDefault(c => c.OtherUser?.Id == userId);
        if (match is not null)
            SelectedConversation = match;   // triggers load + group join
    }

    public void Dispose() => WeakReferenceMessenger.Default.Unregister<PrivateMessageReceived>(this);
}

public partial class RecipientCandidate : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }

    public string Initial =>
        string.IsNullOrEmpty(Username) ? "?" : Username.Substring(0, 1).ToUpperInvariant();
}