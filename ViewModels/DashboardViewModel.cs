using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public VpnEngineService Engine => VpnEngineService.Instance;
    public LocalizationService Loc => LocalizationService.Instance;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasNotification = false;

    public ServerNode? ActiveServer => Engine.ActiveServer;

    public DashboardViewModel()
    {
        Engine.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Engine.ActiveServer))
            {
                OnPropertyChanged(nameof(ActiveServer));
            }
        };

        Loc.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(string.Empty);
        };
    }

    [RelayCommand]
    public async Task ToggleConnectionAsync()
    {
        if (Engine.IsConnected)
        {
            Engine.Disconnect();
            ShowToast(Loc["StatusDisconnected"]);
        }
        else
        {
            var target = ActiveServer;
            if (target == null)
            {
                target = SubscriptionService.Instance.Subscriptions.FirstOrDefault()?.Servers.FirstOrDefault()
                         ?? SubscriptionService.Instance.JsonConfigurations.FirstOrDefault()?.Servers.FirstOrDefault();
            }

            if (target != null)
            {
                await Engine.ConnectAsync(target);
                ShowToast(Loc["StatusConnected"]);
            }
            else
            {
                ShowToast(Loc["ErrorNoServersAvailable"], isError: true);
            }
        }
    }

    [RelayCommand]
    public async Task SelectServer(ServerNode? node)
    {
        if (node != null)
        {
            await Engine.SwitchServerAsync(node);
            OnPropertyChanged(nameof(ActiveServer));
        }
    }

    public void ShowToast(string message, bool isError = false)
    {
        ToastService.Instance.Show(message, isError: isError);
    }
}
