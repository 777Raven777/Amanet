using Avalonia.Media.Imaging;
using Avalonia.Media.Imaging;
using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvaloniaApplication1.ViewModels;

// Opened for a single channel. Construct directly, the way ServersSectionViewModel
// constructs ServerDetailViewModel:
//   var chat = new ChannelChatViewModel(_service, ServerId, channel.Id, channel.Name);
//   _ = chat.LoadAsync();
public partial class ChannelChatViewModel : ViewModelBase,
    IRecipient<ChannelMessageReceived>, IDisposable
{
    private readonly IServerService _service;
    private readonly IChatHub _hub;
    private readonly IProfileService _profileService;

    public Guid ServerId { get; }
    public Guid ChannelId { get; }
    public string ChannelName { get; }

    public ObservableCollection<MessageItem> Messages { get; } = [];

    private readonly Dictionary<string, Bitmap?> _avatarCache = new();

    private async Task LoadAvatarAsync(MessageItem item)
    {
        var url = item.Dto.Sender?.ProfilePictureUrl;
        if (string.IsNullOrEmpty(url)) return;

        if (_avatarCache.TryGetValue(url, out var cached))
        {
            item.Avatar = cached;
            return;
        }

        var bytes = await _profileService.DownloadAsync(url);
        if (bytes is null) { _avatarCache[url] = null; return; }

        using var ms = new MemoryStream(bytes);
        var bmp = new Bitmap(ms);
        _avatarCache[url] = bmp;
        item.Avatar = bmp;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = string.Empty;

    private Guid? _cursor;
    private bool _hasMore;

    public ChannelChatViewModel(IServerService service, IChatHub hub, Guid serverId, Guid channelId, string channelName, IProfileService profileService)
    {
        _service = service;
        _hub = hub;
        ServerId = serverId;
        ChannelId = channelId;
        ChannelName = channelName;
        _profileService = profileService;
    }

    public async Task LoadAsync()
    {
        Messages.Clear();
        _cursor = null;
        _hasMore = false;
        await LoadMessagesAsync(initial: true);

        WeakReferenceMessenger.Default.Register(this);   // start listening
        await _hub.ConnectAsync();                        // idempotent — safe if already connected
        await _hub.JoinChannelAsync(ChannelId);           // join the group for this channel
    }
    private async Task LoadMessagesAsync(bool initial)
    {
        try
        {
            var page = await _service.GetChannelMessagesAsync(ServerId, ChannelId, initial ? null : _cursor);
            if (page is null) { StatusMessage = "Couldn't load messages."; return; }

            foreach (var m in page.Messages)
            {
                var item = new MessageItem { Dto = m };
                Messages.Insert(0, item);
                _ = LoadAvatarAsync(item);
            }

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

    public void Receive(ChannelMessageReceived message)
    {
        var m = message.Message;
        if (m.SourceId != ChannelId) return;
        if (Messages.Any(x => x.Id == m.Id)) return;

        var item = new MessageItem { Dto = m };
        Messages.Add(item);
        _ = LoadAvatarAsync(item);
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0) return;

        try
        {
            await _hub.SendChannelMessageAsync(ChannelId, ServerId, text);
            Draft = string.Empty;
            // NO local Messages.Add — the hub echoes it back to us via the group,
            // and Receive() appends it. Adding here too would double it.
        }
        catch (Exception) { StatusMessage = "The message wouldn't post."; }
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<ChannelMessageReceived>(this);
        _ = _hub.LeaveChannelAsync(ChannelId);
        foreach (var bmp in _avatarCache.Values) bmp?.Dispose();
        _avatarCache.Clear();
    }
}

public partial class MessageItem : ObservableObject
{
    public required MessageDTO Dto { get; init; }

    public Guid Id => Dto.Id;
    public string Message => Dto.Message;
    public DateTime SentAt => Dto.SentAt;
    public string Username => Dto.Sender?.Username ?? "?";
    public string Initial => string.IsNullOrEmpty(Username) ? "?" : Username.Substring(0, 1).ToUpperInvariant();

    public bool Edited => Dto.Edited;

    [ObservableProperty] private Bitmap? _avatar;
}