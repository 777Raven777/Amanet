using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

// Opened for a single channel. Construct directly, the way ServersSectionViewModel
// constructs ServerDetailViewModel:
//   var chat = new ChannelChatViewModel(_service, ServerId, channel.Id, channel.Name);
//   _ = chat.LoadAsync();
public partial class ChannelChatViewModel : ViewModelBase
{
    private readonly IServerService _service;

    public Guid ServerId { get; }
    public Guid ChannelId { get; }
    public string ChannelName { get; }

    public ObservableCollection<MessageDTO> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = string.Empty;

    private Guid? _cursor;
    private bool _hasMore;

    public ChannelChatViewModel(IServerService service, Guid serverId, Guid channelId, string channelName)
    {
        _service = service;
        ServerId = serverId;
        ChannelId = channelId;
        ChannelName = channelName;
    }

    public async Task LoadAsync()
    {
        Messages.Clear();
        _cursor = null;
        _hasMore = false;
        await LoadMessagesAsync(initial: true);
    }

    private async Task LoadMessagesAsync(bool initial)
    {
        try
        {
            var page = await _service.GetChannelMessagesAsync(ServerId, ChannelId, initial ? null : _cursor);
            if (page is null) { StatusMessage = "Couldn't load messages."; return; }

            // newest-first from the server → insert at 0 for oldest→newest top-to-bottom
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
        if (_hasMore) await LoadMessagesAsync(initial: false);
    }

    private bool CanSend => !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0) return;

        try
        {
            var msg = await _service.SendChannelMessageAsync(ServerId, ChannelId, text);
            if (msg is null) { StatusMessage = "The message wouldn't post."; return; }

            Messages.Add(msg);            // newest to the bottom
            Draft = string.Empty;
        }
        catch (HttpRequestException) { StatusMessage = "Couldn't reach the server."; }
    }
}