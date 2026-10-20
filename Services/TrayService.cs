using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using aura.Models;

namespace aura.Services;

public class TrayService
{
    private static TrayService? _instance;
    public static TrayService Instance => _instance ??= new TrayService();

    private TrayIcon? _trayIcon;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _connectItem;
    private NativeMenuItem? _switchServerMenu;
    private Window? _mainWindow;
    private bool _isInitialized = false;

    public void Initialize(Window mainWindow)
    {
        if (_isInitialized) return;
        _mainWindow = mainWindow;
        _isInitialized = true;

        WindowIcon? icon = null;
        try
        {
            string iconPng = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "aura.png");
            if (File.Exists(iconPng))
            {
                using var stream = File.OpenRead(iconPng);
                icon = new WindowIcon(new Bitmap(stream));
            }
            else if (File.Exists("Assets/aura.png"))
            {
                using var stream = File.OpenRead("Assets/aura.png");
                icon = new WindowIcon(new Bitmap(stream));
            }
        }
        catch { }

        _statusItem = new NativeMenuItem { IsEnabled = false };
        _connectItem = new NativeMenuItem();
        _connectItem.Click += async (s, e) => await ToggleConnectionAsync();

        _switchServerMenu = new NativeMenuItem("Сменить сервер")
        {
            Menu = new NativeMenu()
        };

        var importItem = new NativeMenuItem("Импорт подписки из буфера обмена");
        importItem.Click += async (s, e) => await ImportFromClipboardAsync();

        var showItem = new NativeMenuItem("Показать окно");
        showItem.Click += (s, e) => ShowMainWindow();

        var exitItem = new NativeMenuItem("Выйти");
        exitItem.Click += (s, e) => ExitApplication();

        var menu = new NativeMenu
        {
            _statusItem,
            new NativeMenuItemSeparator(),
            _connectItem,
            _switchServerMenu,
            importItem,
            new NativeMenuItemSeparator(),
            showItem,
            exitItem
        };

        _trayIcon = new TrayIcon
        {
            ToolTipText = "lumen aura",
            Menu = menu,
            IsVisible = true
        };

        if (icon != null)
        {
            _trayIcon.Icon = icon;
        }

        _trayIcon.Clicked += (s, e) => ShowMainWindow();

        var app = Application.Current;
        if (app != null)
        {
            var icons = TrayIcon.GetIcons(app);
            if (icons == null)
            {
                icons = new TrayIcons();
                TrayIcon.SetIcons(app, icons);
            }
            icons.Add(_trayIcon);
        }

        // Subscriptions to sync UI & Tray state
        VpnEngineService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VpnEngineService.IsConnected) ||
                e.PropertyName == nameof(VpnEngineService.IsConnecting) ||
                e.PropertyName == nameof(VpnEngineService.ActiveServer))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(UpdateMenu);
            }
        };

        SubscriptionService.Instance.Subscriptions.CollectionChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateMenu);
        };
        SubscriptionService.Instance.JsonConfigurations.CollectionChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateMenu);
        };

        UpdateMenu();
    }

    public void UpdateMenu()
    {
        if (_statusItem == null || _connectItem == null || _switchServerMenu == null || _trayIcon == null)
            return;

        var engine = VpnEngineService.Instance;
        var active = engine.ActiveServer;
        string serverName = active?.DisplayName ?? (!string.IsNullOrEmpty(active?.Name) ? active.Name : "Не выбран");

        if (engine.IsConnected)
        {
            _statusItem.Header = $"Подключено: {serverName}";
            _connectItem.Header = "Отключиться";
            _trayIcon.ToolTipText = $"lumen aura — Подключено ({serverName})";
        }
        else if (engine.IsConnecting)
        {
            _statusItem.Header = $"Подключение: {serverName}";
            _connectItem.Header = "Отключиться";
            _trayIcon.ToolTipText = $"lumen aura — Подключение ({serverName})";
        }
        else
        {
            _statusItem.Header = $"Выбрано: {serverName}";
            _connectItem.Header = "Подключиться";
            _trayIcon.ToolTipText = $"lumen aura — Выбрано ({serverName})";
        }

        // Populate 'Сменить сервер' with servers from active server's subscription
        Subscription? targetSub = null;
        if (active != null)
        {
            targetSub = SubscriptionService.Instance.Subscriptions.FirstOrDefault(s => s.Servers.Any(srv => srv.Id == active.Id))
                     ?? SubscriptionService.Instance.JsonConfigurations.FirstOrDefault(s => s.Servers.Any(srv => srv.Id == active.Id));
        }

        if (targetSub == null)
        {
            targetSub = SubscriptionService.Instance.Subscriptions.FirstOrDefault()
                     ?? SubscriptionService.Instance.JsonConfigurations.FirstOrDefault();
        }

        if (_switchServerMenu.Menu == null)
        {
            _switchServerMenu.Menu = new NativeMenu();
        }

        _switchServerMenu.Menu.Items.Clear();

        if (targetSub != null && targetSub.Servers.Count > 0)
        {
            _switchServerMenu.IsEnabled = true;
            _switchServerMenu.Header = $"Сменить сервер ({targetSub.Name})";

            foreach (var server in targetSub.Servers)
            {
                var node = server;
                bool isCurrent = active != null && node.Id == active.Id;
                string prefix = isCurrent ? "● " : "○ ";
                var item = new NativeMenuItem($"{prefix}{node.DisplayName}");
                item.Click += async (s, e) =>
                {
                    await engine.SwitchServerAsync(node);
                    UpdateMenu();
                };
                _switchServerMenu.Menu.Items.Add(item);
            }
        }
        else
        {
            _switchServerMenu.IsEnabled = false;
            _switchServerMenu.Header = "Сменить сервер";
        }
    }

    private async Task ToggleConnectionAsync()
    {
        var engine = VpnEngineService.Instance;
        if (engine.IsConnected || engine.IsConnecting)
        {
            engine.Disconnect();
            ToastService.Instance.Show(LocalizationService.Instance["StatusDisconnected"]);
        }
        else
        {
            var target = engine.ActiveServer
                      ?? SubscriptionService.Instance.Subscriptions.FirstOrDefault()?.Servers.FirstOrDefault()
                      ?? SubscriptionService.Instance.JsonConfigurations.FirstOrDefault()?.Servers.FirstOrDefault();

            if (target != null)
            {
                await engine.ConnectAsync(target);
                ToastService.Instance.Show(LocalizationService.Instance["StatusConnected"]);
            }
            else
            {
                ToastService.Instance.Show(LocalizationService.Instance["ErrorNoServersAvailable"], isError: true);
            }
        }
    }

    private async Task ImportFromClipboardAsync()
    {
        try
        {
            if (_mainWindow?.Clipboard is { } cb)
            {
                var text = await cb.TryGetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Trim();
                    if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        var sub = await SubscriptionService.Instance.AddSubscriptionAsync("", text);
                        ToastService.Instance.Show($"Подписка '{sub.Name}' добавлена ({sub.Servers.Count} серв.)");
                        ShowMainWindow();
                        return;
                    }
                    else if (text.Contains("://") || text.StartsWith('{') || text.Contains("[Interface]"))
                    {
                        SubscriptionService.Instance.AddRawConfigSubscription("", text);
                        ToastService.Instance.Show("Конфигурация импортирована из буфера");
                        ShowMainWindow();
                        return;
                    }
                }
            }
            ToastService.Instance.Show("В буфере обмена нет ссылки или конфигурации", isError: true);
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show("Ошибка импорта: " + ex.Message, isError: true);
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    public bool IsExiting { get; private set; } = false;

    public void ExitApplication()
    {
        IsExiting = true;
        try
        {
            VpnEngineService.Instance.Disconnect();
        }
        catch { }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }
}
