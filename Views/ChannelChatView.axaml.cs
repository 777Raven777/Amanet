using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaApplication1.ViewModels;
using System.Collections.Specialized;

namespace AvaloniaApplication1.Views;

public partial class ChannelChatView : UserControl
{
    public ChannelChatView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ChannelChatViewModel vm)
            vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        Dispatcher.UIThread.Post(() =>
            this.FindControl<ScrollViewer>("ThreadScroll")?.ScrollToEnd(),
            DispatcherPriority.Background);
    }
}