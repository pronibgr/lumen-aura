using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class RoutingViewModel : ViewModelBase
{
    public LocalizationService Loc => LocalizationService.Instance;
    public SettingsService SetService => SettingsService.Instance;

    [ObservableProperty]
    private int _routingMode = 0; // 0 = All, 1 = Bypass, 2 = Only selected

    public bool IsRoutingAll => RoutingMode == 0;
    public bool IsRoutingBypass => RoutingMode == 1;
    public bool IsRoutingOnly => RoutingMode == 2;

    [ObservableProperty]
    private bool _isAddAppDialogOpen = false;

    [ObservableProperty]
    private string _searchAppText = string.Empty;

    [ObservableProperty]
    private string _customAppName = string.Empty;

    [ObservableProperty]
    private bool _showSystemApps = false;

    private readonly System.Collections.Generic.List<ProcessItem> _allAvailableApplications = new();

    public ObservableCollection<ProcessItem> SelectedApplications { get; } = new();
    public ObservableCollection<ProcessItem> AvailableApplications { get; } = new();

    public RoutingViewModel()
    {
        var settings = SetService.GetSettings();
        RoutingMode = settings.RoutingMode;

        // Load only user's saved apps (No hardcoded apps per Requirement 4)
        foreach (var app in settings.SelectedApplications)
        {
            SelectedApplications.Add(app);
        }

        RefreshAvailableApplications();

        Loc.LanguageChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    partial void OnSearchAppTextChanged(string value)
    {
        FilterAvailableApplications();
    }

    partial void OnShowSystemAppsChanged(bool value)
    {
        RefreshAvailableApplications();
    }

    partial void OnRoutingModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsRoutingAll));
        OnPropertyChanged(nameof(IsRoutingBypass));
        OnPropertyChanged(nameof(IsRoutingOnly));
    }

    public void RefreshAvailableApplications()
    {
        _allAvailableApplications.Clear();
        var procs = ProcessService.Instance.GetRunningApplications(ShowSystemApps);
        foreach (var p in procs)
        {
            if (!SelectedApplications.Any(a => a.Name.Equals(p.Name, System.StringComparison.OrdinalIgnoreCase)))
            {
                _allAvailableApplications.Add(p);
            }
        }
        FilterAvailableApplications();
    }

    private void FilterAvailableApplications()
    {
        AvailableApplications.Clear();
        var query = SearchAppText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? (System.Collections.Generic.IEnumerable<ProcessItem>)_allAvailableApplications
            : _allAvailableApplications.Where(p =>
                (p.DisplayName != null && p.DisplayName.Contains(query, System.StringComparison.OrdinalIgnoreCase)) ||
                (p.Name != null && p.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase)) ||
                (p.Category != null && p.Category.Contains(query, System.StringComparison.OrdinalIgnoreCase)) ||
                (p.Path != null && p.Path.Contains(query, System.StringComparison.OrdinalIgnoreCase)));

        foreach (var item in filtered)
        {
            AvailableApplications.Add(item);
        }
    }

    [RelayCommand]
    public void SetMode(object? param)
    {
        if (param != null && int.TryParse(param.ToString(), out int mode))
        {
            RoutingMode = mode;
            var s = SetService.GetSettings();
            s.RoutingMode = mode;
            SetService.SaveSettings(s);
            OnPropertyChanged(nameof(IsRoutingAll));
            OnPropertyChanged(nameof(IsRoutingBypass));
            OnPropertyChanged(nameof(IsRoutingOnly));

            if (VpnEngineService.Instance.IsConnected)
            {
                _ = VpnEngineService.Instance.ReloadRoutingAsync();
            }
        }
    }

    [RelayCommand]
    public void OpenAddAppDialog()
    {
        SearchAppText = string.Empty;
        CustomAppName = string.Empty;
        RefreshAvailableApplications();
        IsAddAppDialogOpen = true;
    }

    [RelayCommand]
    public void CloseAddAppDialog()
    {
        IsAddAppDialogOpen = false;
    }

    [RelayCommand]
    public void AddApplication(ProcessItem item)
    {
        if (item != null && !SelectedApplications.Any(a => a.Name.Equals(item.Name, System.StringComparison.OrdinalIgnoreCase)))
        {
            SelectedApplications.Add(item);
            SaveApps();
            RefreshAvailableApplications();
        }
        IsAddAppDialogOpen = false;
    }

    [RelayCommand]
    public void AddCustomApplication()
    {
        if (!string.IsNullOrWhiteSpace(CustomAppName))
        {
            var item = new ProcessItem
            {
                Name = CustomAppName.Trim().ToLowerInvariant(),
                DisplayName = CustomAppName.Trim(),
                Category = "Custom User App",
                Path = CustomAppName.Trim()
            };
            AddApplication(item);
        }
    }

    [RelayCommand]
    public void RemoveApplication(ProcessItem item)
    {
        if (item != null)
        {
            SelectedApplications.Remove(item);
            SaveApps();
            RefreshAvailableApplications();
        }
    }

    private void SaveApps()
    {
        var s = SetService.GetSettings();
        s.SelectedApplications.Clear();
        foreach (var app in SelectedApplications)
        {
            s.SelectedApplications.Add(app);
        }
        SetService.SaveSettings(s);

        if (VpnEngineService.Instance.IsConnected)
        {
            _ = VpnEngineService.Instance.ReloadRoutingAsync();
        }
    }
}
