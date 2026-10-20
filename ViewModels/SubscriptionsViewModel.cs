using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class SubscriptionsViewModel : ViewModelBase
{
    public SubscriptionService SubService => SubscriptionService.Instance;
    public LocalizationService Loc => LocalizationService.Instance;
    public SettingsService SetService => SettingsService.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isAddDialogOpen = false;

    [ObservableProperty]
    private bool _isAdding = false;

    // 0 = Subscription, 1 = JSON
    [ObservableProperty]
    private int _selectedImportType = 0;

    public bool IsImportTypeSubscription => SelectedImportType == 0;
    public bool IsImportTypeJson => SelectedImportType == 1;

    [ObservableProperty]
    private string _newSubName = string.Empty;

    [ObservableProperty]
    private string _newSubUrl = string.Empty;

    [ObservableProperty]
    private string _rawConfigInput = string.Empty;

    [ObservableProperty]
    private string _clipboardToast = string.Empty;

    [ObservableProperty]
    private bool _showClipboardToast = false;

    [ObservableProperty]
    private ServerNode? _selectedServer;

    public List<string> ImportTypeOptions => Loc.CurrentLanguage == "ru"
        ? new List<string> { "Подписка", "JSON" }
        : new List<string> { "Subscription", "JSON" };

    public string PingDisplayMode => SetService.GetSettings().PingDisplayMode;

    public SubscriptionsViewModel()
    {
        Loc.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(ImportTypeOptions));
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        foreach (var sub in SubService.Subscriptions)
        {
            sub.ApplyFilter(value);
        }
        foreach (var sub in SubService.JsonConfigurations)
        {
            sub.ApplyFilter(value);
        }
    }

    partial void OnSelectedImportTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsImportTypeSubscription));
        OnPropertyChanged(nameof(IsImportTypeJson));
    }

    [RelayCommand]
    public void SelectImportType(object? param)
    {
        if (param != null && int.TryParse(param.ToString(), out int val))
        {
            SelectedImportType = val;
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void ToggleExpandSubscription(Subscription sub)
    {
        sub.IsExpanded = !sub.IsExpanded;
    }

    [RelayCommand]
    public async Task PingAllServersAsync(Subscription sub)
    {
        string pingType = SetService.GetSettings().PingType;
        await SubService.PingAllServersInSubscriptionAsync(sub, pingType);
        ShowMessage(Loc["PingAllServers"] + " ✓");
    }

    [RelayCommand]
    public async Task UpdateSubscriptionAsync(Subscription sub)
    {
        await SubService.UpdateSubscriptionAsync(sub);
        sub.ApplyFilter(SearchText);
        ShowMessage(Loc["UpdateSubscription"] + " ✓");
    }

    [RelayCommand]
    public void PinSubscription(Subscription sub)
    {
        SubService.PinSubscription(sub);
        ShowMessage(sub.IsPinned ? Loc["PinSubscription"] : Loc["UnpinSubscription"]);
    }

    [RelayCommand]
    public async Task CopySubscriptionUrl(Subscription sub)
    {
        if (sub == null) return;
        string textToCopy = !string.IsNullOrWhiteSpace(sub.Url) ? sub.Url : (sub.Servers.FirstOrDefault()?.RawConfig ?? "");
        
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null && !string.IsNullOrEmpty(textToCopy))
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(textToCopy);
        }

        ShowMessage(Loc["UrlCopiedNotification"]);
    }

    [RelayCommand]
    public void DeleteSubscription(Subscription sub)
    {
        SubService.RemoveSubscription(sub);
        if (SelectedServer != null && (SelectedServer.SubscriptionId == sub.Id || sub.Servers.Any(s => s.Id == SelectedServer.Id || s == SelectedServer)))
        {
            SelectedServer = null;
        }
        ShowMessage(Loc["DeleteSubscription"] + " ✓");
    }

    [RelayCommand]
    public async Task SelectServerNode(ServerNode node)
    {
        if (node == null) return;
        SelectedServer = node;
        foreach (var sub in SubService.Subscriptions.Concat(SubService.JsonConfigurations))
        {
            foreach (var s in sub.Servers)
            {
                s.IsSelected = (s.Id == node.Id);
            }
        }
        ShowMessage($"{Loc["SelectedNode"]}: {node.DisplayName}");

        await VpnEngineService.Instance.SwitchServerAsync(node);
    }

    [RelayCommand]
    public async Task PingSingleServerAsync(ServerNode node)
    {
        node.IsPinging = true;
        node.NotifyPingUpdated();
        string pingType = SetService.GetSettings().PingType;
        var res = await NetworkService.Instance.PingHostAsync(node.Host, node.Port, pingType);
        node.HasPinged = true;
        node.PingSuccess = res.Success;
        node.PingMs = res.LatencyMs;
        node.IsPinging = false;
        node.NotifyPingUpdated();
    }

    [RelayCommand]
    public async Task CopyServerJson(ServerNode? node)
    {
        if (node == null) return;
        string textToCopy = !string.IsNullOrWhiteSpace(node.RawConfig)
            ? node.RawConfig
            : System.Text.Json.JsonSerializer.Serialize(node, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(textToCopy);
            }
            else if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var topLevel = TopLevel.GetTopLevel(singleView.MainView);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(textToCopy);
                }
            }
        }
        catch { }

        ToastService.Instance.Show(Loc["JsonCopied"]);
    }

    [RelayCommand]
    public void DeleteServer(ServerNode? node)
    {
        if (node == null) return;

        foreach (var sub in SubService.Subscriptions.Concat(SubService.JsonConfigurations))
        {
            var item = sub.Servers.FirstOrDefault(s => s.Id == node.Id);
            if (item != null)
            {
                sub.Servers.Remove(item);
                sub.NotifyServersChanged();
                SubService.SaveSubscriptions();
                break;
            }
        }

        if (SelectedServer?.Id == node.Id)
        {
            SelectedServer = null;
        }

        if (VpnEngineService.Instance.ActiveServer?.Id == node.Id)
        {
            VpnEngineService.Instance.ActiveServer = null;
        }

        ToastService.Instance.Show(Loc["ServerDeleted"]);
    }

    [RelayCommand]
    public void OpenAddDialog()
    {
        NewSubName = string.Empty;
        NewSubUrl = string.Empty;
        RawConfigInput = string.Empty;
        SelectedImportType = 0;
        IsAddDialogOpen = true;
    }

    [RelayCommand]
    public void CloseAddDialog()
    {
        IsAddDialogOpen = false;
    }

    [RelayCommand]
    public async Task ConfirmAddSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSubUrl))
            return;

        IsAdding = true;
        try
        {
            var sub = await SubService.AddSubscriptionAsync(NewSubName, NewSubUrl);
            sub.ApplyFilter(SearchText);
            IsAddDialogOpen = false;
            ShowMessage(Loc.CurrentLanguage == "ru"
                ? $"Подписка '{sub.Name}' добавлена ({sub.Servers.Count} серверов)"
                : $"Subscription '{sub.Name}' added ({sub.Servers.Count} nodes)");
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
        finally
        {
            IsAdding = false;
        }
    }

    [RelayCommand]
    public void ConfirmImportJson()
    {
        if (string.IsNullOrWhiteSpace(RawConfigInput))
            return;

        try
        {
            SubService.AddRawConfigSubscription(NewSubName, RawConfigInput);
            IsAddDialogOpen = false;
            ShowMessage(Loc.CurrentLanguage == "ru" ? "Конфигурация успешно импортирована" : "Configuration imported successfully");
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
    }

    [RelayCommand]
    public async Task UploadAwgConfigFileAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.StorageProvider is { } storage)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select AmneziaWG / WireGuard config (.conf)",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Config files") { Patterns = new[] { "*.conf", "*.json", "*.txt" } },
                        new("All files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    await using var stream = await files[0].OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    RawConfigInput = await reader.ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(NewSubName))
                    {
                        NewSubName = Path.GetFileNameWithoutExtension(files[0].Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage("Upload error: " + ex.Message);
        }
    }

    public void ShowMessage(string msg, bool isError = false)
    {
        ToastService.Instance.Show(msg, isError: isError);
    }
}
