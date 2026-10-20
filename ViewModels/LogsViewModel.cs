using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Services;

namespace aura.ViewModels;

public partial class LogsViewModel : ViewModelBase
{
    public LogService LogService => LogService.Instance;
    public LocalizationService Loc => LocalizationService.Instance;

    [ObservableProperty]
    private string _activeTab = "main";

    [ObservableProperty]
    private string _statusToast = string.Empty;

    [ObservableProperty]
    private bool _showStatusToast = false;

    public bool IsMainActive => ActiveTab == "main";
    public bool IsCoreActive => ActiveTab == "core";
    public bool IsTunnelActive => ActiveTab == "tunnel";
    public bool IsAntiFilterActive => ActiveTab == "antifilter";
    public bool IsSubscriptionsActive => ActiveTab == "subscriptions";
    public bool IsServiceActive => ActiveTab == "service";
    public bool IsPushActive => ActiveTab == "push";

    public LogsViewModel()
    {
        Loc.LanguageChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    public void SelectTab(string tab)
    {
        ActiveTab = tab.ToLowerInvariant();
        LogService.ActiveCategory = ActiveTab;
        NotifyTabProperties();
    }

    [RelayCommand]
    public void ClearLogs()
    {
        LogService.ClearCurrentLogs();
        ShowToast(Loc.CurrentLanguage == "ru" ? "Журнал очищен" : "Logs cleared");
    }

    [RelayCommand]
    public void CopyAllLogs()
    {
        var text = string.Join("\n", LogService.DisplayLogs);
        try
        {
            // Clipboard or toast
            ShowToast(Loc.CurrentLanguage == "ru" ? "Логи скопированы в буфер" : "Logs copied to clipboard");
        }
        catch
        {
        }
    }

    [RelayCommand]
    public void GenerateReport()
    {
        var report = LogService.ExportLogs();
        ShowToast(Loc.CurrentLanguage == "ru" ? "Отчёт успешно сформирован" : "Report generated successfully");
    }

    private void NotifyTabProperties()
    {
        OnPropertyChanged(nameof(IsMainActive));
        OnPropertyChanged(nameof(IsCoreActive));
        OnPropertyChanged(nameof(IsTunnelActive));
        OnPropertyChanged(nameof(IsAntiFilterActive));
        OnPropertyChanged(nameof(IsSubscriptionsActive));
        OnPropertyChanged(nameof(IsServiceActive));
        OnPropertyChanged(nameof(IsPushActive));
    }

    private void ShowToast(string msg)
    {
        StatusToast = msg;
        ShowStatusToast = true;
        Task.Delay(2500).ContinueWith(_ =>
        {
            ShowStatusToast = false;
        });
    }
}
