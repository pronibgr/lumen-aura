using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Models;

public partial class ProcessItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _category = "Application";

    [ObservableProperty]
    private bool _isSelected = false;

    [ObservableProperty]
    private bool _isSystemApp = false;

    [ObservableProperty]
    private string _packageName = string.Empty;
}
