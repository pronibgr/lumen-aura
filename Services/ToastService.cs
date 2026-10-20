using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Services;

public partial class ToastService : ObservableObject
{
    private static ToastService? _instance;
    public static ToastService Instance => _instance ??= new ToastService();

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isVisible = false;

    [ObservableProperty]
    private bool _isError = false;

    private CancellationTokenSource? _cts;

    public void Show(string message, int durationMs = 2400, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Message = message.Trim();
        IsError = isError;
        IsVisible = true;

        Task.Delay(durationMs, token).ContinueWith(t =>
        {
            if (!token.IsCancellationRequested)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsVisible = false;
                });
            }
        });
    }
}
