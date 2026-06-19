using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaApplication1.ViewModels;
using System.Collections.Specialized;
namespace AvaloniaApplication1.Views;

public partial class ConversationsSectionView : UserControl
{
    public ConversationsSectionView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ConversationsSectionViewModel vm)
            vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // Defer until layout has placed the new item, else ScrollToEnd targets the old extent.
        Dispatcher.UIThread.Post(() =>
            this.FindControl<ScrollViewer>("MessageScroll")?.ScrollToEnd(),
            DispatcherPriority.Background);
    }
}