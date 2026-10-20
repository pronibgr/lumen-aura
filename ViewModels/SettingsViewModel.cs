using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public LocalizationService Loc => LocalizationService.Instance;
    public SettingsService SetService => SettingsService.Instance;

    [ObservableProperty]
    private AppSettings _settings;

    // Submenu navigation inside Settings:
    // 0: General, 1: Routing, 2: Advanced, 3: Subscriptions, 4: Ping, 5: Network, 6: Logs (on phone)
    [ObservableProperty]
    private int _selectedSubMenuIndex = 0;

    [ObservableProperty]
    private bool _isPhoneMode = false;

    // 0 = Light, 1 = Dark
    [ObservableProperty]
    private int _selectedThemeIndex = 0;

    public bool IsGeneralActive => SelectedSubMenuIndex == 0;
    public bool IsRoutingActive => SelectedSubMenuIndex == 1;
    public bool IsAdvancedActive => SelectedSubMenuIndex == 2;
    public bool IsSubscriptionsActive => SelectedSubMenuIndex == 3;
    public bool IsPingActive => SelectedSubMenuIndex == 4;
    public bool IsNetworkActive => SelectedSubMenuIndex == 5;
    public bool IsLogsActive => SelectedSubMenuIndex == 6;

    public RoutingViewModel RoutingVM { get; }
    public LogsViewModel? LogsVM { get; set; }

    public System.Action? RequestCloseSettings { get; set; }

    public System.Collections.ObjectModel.ObservableCollection<string> AvailableThemes { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<string> AvailableSpeedUnits { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<string> AvailablePingDisplayModes { get; } = new();

    public List<string> AvailableLanguages { get; } = new() { "English", "Русский" };

    public List<string> AvailableUserAgents { get; } = new()
    {
        "aura/1.0 (Clash.Meta/1.18.0; v2rayN/6.39)",
        "v2rayN/6.39",
        "Clash.Meta/1.18.0",
        "sing-box/1.9.0",
        "Happ/3.6.0 (Windows NT 10.0; Win64; x64)",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0.0.0",
        "Custom"
    };

    public List<string> TunProviders { get; } = new()
    {
        "Xray TUN",
        "sing-box TUN",
        "wintun",
        "system"
    };

    public List<string> ViaProxyModes { get; } = new()
    {
        "default",
        "double",
        "keepalive"
    };

    public int SelectedSpeedUnitIndex
    {
        get => Settings.SpeedUnit == "Bits" ? 1 : 0;
        set
        {
            string newUnit = value == 1 ? "Bits" : "Bytes";
            if (Settings.SpeedUnit != newUnit)
            {
                Settings.SpeedUnit = newUnit;
                SetService.SaveSettings(Settings);
                OnPropertyChanged(nameof(SelectedSpeedUnitIndex));
            }
        }
    }

    public int SelectedPingDisplayModeIndex
    {
        get => Settings.PingDisplayMode == "Icon" ? 1 : 0;
        set
        {
            string newMode = value == 1 ? "Icon" : "Number";
            if (Settings.PingDisplayMode != newMode)
            {
                Settings.PingDisplayMode = newMode;
                SetService.SaveSettings(Settings);
                OnPropertyChanged(nameof(SelectedPingDisplayModeIndex));
            }
        }
    }

    public string SelectedLanguageDisplay
    {
        get => Settings.Language == "ru" ? "Русский" : "English";
        set
        {
            string langCode = (value == "Русский") ? "ru" : "en";
            if (Settings.Language != langCode)
            {
                Settings.Language = langCode;
                Loc.CurrentLanguage = langCode;
                SetService.SaveSettings(Settings);
                RefreshLocalizedCollections();
                OnPropertyChanged(nameof(SelectedLanguageDisplay));
            }
        }
    }

    public SettingsViewModel(RoutingViewModel? routingVM = null)
    {
        RoutingVM = routingVM ?? new RoutingViewModel();
        _settings = SetService.GetSettings();
        _settings.CurrentLanIp = NetworkService.Instance.GetLocalIpAddress();

        if (Avalonia.Application.Current != null)
        {
            _selectedThemeIndex = Avalonia.Application.Current.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? 1 : 0;
        }

        RefreshLocalizedCollections();

        Loc.LanguageChanged += (s, e) =>
        {
            RefreshLocalizedCollections();
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(SelectedLanguageDisplay));
        };
    }

    public void RefreshLocalizedCollections()
    {
        int savedTheme = SelectedThemeIndex;
        AvailableThemes.Clear();
        AvailableThemes.Add(Loc["ThemeLight"]);
        AvailableThemes.Add(Loc["ThemeDark"]);
        SelectedThemeIndex = savedTheme;

        int savedSpeed = SelectedSpeedUnitIndex;
        AvailableSpeedUnits.Clear();
        AvailableSpeedUnits.Add(Loc["SpeedUnitBytes"]);
        AvailableSpeedUnits.Add(Loc["SpeedUnitBits"]);

        int savedPing = SelectedPingDisplayModeIndex;
        AvailablePingDisplayModes.Clear();
        AvailablePingDisplayModes.Add(Loc["PingDisplayNumber"]);
        AvailablePingDisplayModes.Add(Loc["PingDisplayIcon"]);

        OnPropertyChanged(nameof(AvailableThemes));
        OnPropertyChanged(nameof(AvailableSpeedUnits));
        OnPropertyChanged(nameof(AvailablePingDisplayModes));
        OnPropertyChanged(nameof(SelectedThemeIndex));
        OnPropertyChanged(nameof(SelectedSpeedUnitIndex));
        OnPropertyChanged(nameof(SelectedPingDisplayModeIndex));
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = value == 1 ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        }
    }

    partial void OnSelectedSubMenuIndexChanged(int value)
    {
        NotifySubMenuChanged();
    }

    [RelayCommand]
    public void SelectSubMenu(object? param)
    {
        if (param != null && int.TryParse(param.ToString(), out int idx))
        {
            SelectedSubMenuIndex = idx;
            NotifySubMenuChanged();
        }
    }

    [RelayCommand]
    public void BackToMain()
    {
        RequestCloseSettings?.Invoke();
    }

    private void NotifySubMenuChanged()
    {
        OnPropertyChanged(nameof(IsGeneralActive));
        OnPropertyChanged(nameof(IsRoutingActive));
        OnPropertyChanged(nameof(IsAdvancedActive));
        OnPropertyChanged(nameof(IsSubscriptionsActive));
        OnPropertyChanged(nameof(IsPingActive));
        OnPropertyChanged(nameof(IsNetworkActive));
        OnPropertyChanged(nameof(IsLogsActive));
    }

    [RelayCommand]
    public void SaveSettings()
    {
        SetService.SaveSettings(Settings);
        ToastService.Instance.Show(Loc["SettingsSaved"]);
    }

    [RelayCommand]
    public void SetPingType(string type)
    {
        Settings.PingType = type;
        SaveSettings();
    }
}
