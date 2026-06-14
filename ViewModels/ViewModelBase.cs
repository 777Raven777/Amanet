using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _statusMessage;

    private CancellationTokenSource? _statusCts;

    partial void OnStatusMessageChanged(string? value)
    {
        _statusCts?.Cancel();
        if (string.IsNullOrEmpty(value)) return;

        _statusCts = new CancellationTokenSource();
        var token = _statusCts.Token;
        _ = Task.Delay(TimeSpan.FromSeconds(4), token).ContinueWith(t =>
        {
            if (!t.IsCanceled) StatusMessage = null;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
