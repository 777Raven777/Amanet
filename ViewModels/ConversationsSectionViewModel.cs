using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

public partial class ConversationsSectionViewModel : ViewModelBase, IActivatable
{
    private readonly IConversationService _service;
    public ConversationsSectionViewModel(IConversationService service) => _service = service;

    public ObservableCollection<ConversationListItemDTO> Conversations { get; } = [];
    public ObservableCollection<MessageDTO> Messages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveConversation))]
    private ConversationListItemDTO? _selectedConversation;

    private Guid? _cursor;
    private bool _hasMore;

    public bool HasActiveConversation => SelectedConversation != null;

    public async Task ActivateAsync()
    {
        try
        {
            var list = await _service.GetConversationsAsync();
            Conversations.Clear();
            foreach (var c in list?.Conversations ?? [])
                Conversations.Add(c);
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }

    partial void OnSelectedConversationChanged(ConversationListItemDTO? value)
    {
        if (value is null) return;
        _cursor = null;
        _hasMore = false;
        Messages.Clear();
        _ = LoadMessagesAsync(value.Id, initial: true);
    }

    private async Task LoadMessagesAsync(Guid conversationId, bool initial)
    {
        try
        {
            var page = await _service.GetMessagesAsync(conversationId, initial ? null : _cursor);
            if (page is null) { StatusMessage = "Couldn't load messages."; return; }

            // Server returns newest-first. Insert at 0 so the collection ends up
            // oldest→newest top-to-bottom (normal chat reading order).
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
}