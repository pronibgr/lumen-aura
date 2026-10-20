using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public LocalizationService Loc => LocalizationService.Instance;
    public VpnEngineService Engine => VpnEngineService.Instance;
    public SettingsService SetService => SettingsService.Instance;
    public ToastService Toast => ToastService.Instance;

    // SelectedDestination: 0 = Servers (default), 1 = Settings, 2 = Logs, 3 = About
    [ObservableProperty]
    private int _selectedDestination = 0;

    // Collapsed = 96dp, Expanded = 220dp
    [ObservableProperty]
    private bool _isToolbarExpanded = false;

    // Theme state: false = Light Green (default), true = Dark Green
    [ObservableProperty]
    private bool _isDarkTheme = false;

    public double ToolbarWidth => IsToolbarExpanded ? 220 : 96;

    public bool IsServersActive => SelectedDestination == 0;
    public bool IsSettingsActive => SelectedDestination == 1;
    public bool IsLogsActive => SelectedDestination == 2;
    public bool IsAboutActive => SelectedDestination == 3;

    public DashboardViewModel DashboardVM { get; }
    public SubscriptionsViewModel SubscriptionsVM { get; }
    public RoutingViewModel RoutingVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public LogsViewModel LogsVM { get; }
    public ProtocolsViewModel ProtocolsVM { get; }

    [ObservableProperty]
    private bool _isPhoneMode = false;

    partial void OnIsPhoneModeChanged(bool value)
    {
        SettingsVM.IsPhoneMode = value;
    }

    public string WindowTitle => "lumen aura";
    public string ShortTitle => "aura";
    public string CurrentLanguageCode => Loc.CurrentLanguage.ToUpperInvariant();

    public MainViewModel()
    {
        DashboardVM = new DashboardViewModel();
        SubscriptionsVM = new SubscriptionsViewModel();
        RoutingVM = new RoutingViewModel();
        LogsVM = new LogsViewModel();
        ProtocolsVM = new ProtocolsViewModel();
        SettingsVM = new SettingsViewModel(RoutingVM)
        {
            LogsVM = LogsVM
        };

        SettingsVM.RequestCloseSettings = () =>
        {
            SelectDestination(0);
        };

        var settings = SetService.GetSettings();
        Loc.CurrentLanguage = settings.Language;

        Loc.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(CurrentLanguageCode));
        };
    }

    public void UpdateAdaptiveWidth(double width)
    {
        bool phone = width > 0 && width < 720;
        if (IsPhoneMode != phone)
        {
            IsPhoneMode = phone;
        }
    }

    partial void OnSelectedDestinationChanged(int value)
    {
        NotifyDestinationProperties();
    }

    [RelayCommand]
    public void SelectDestination(object? param)
    {
        if (param != null && int.TryParse(param.ToString(), out int idx))
        {
            SelectedDestination = idx;
            if (idx == 1)
            {
                SettingsVM.SelectedSubMenuIndex = 0;
            }
            NotifyDestinationProperties();
        }
    }

    private void NotifyDestinationProperties()
    {
        OnPropertyChanged(nameof(IsServersActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsLogsActive));
        OnPropertyChanged(nameof(IsAboutActive));
    }

    [RelayCommand]
    public void OpenAddDialog()
    {
        SubscriptionsVM.OpenAddDialog();
    }

    [RelayCommand]
    public void OpenGitHubUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/pronibgr") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start("xdg-open", "https://github.com/pronibgr");
            }
            catch
            {
            }
        }
    }

    [RelayCommand]
    public void ToggleToolbar()
    {
        IsToolbarExpanded = !IsToolbarExpanded;
        OnPropertyChanged(nameof(ToolbarWidth));
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        SettingsVM.SelectedThemeIndex = value ? 1 : 0;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
