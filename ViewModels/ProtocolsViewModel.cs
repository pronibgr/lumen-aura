using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using aura.Models;
using aura.Services;

namespace aura.ViewModels;

public partial class ProtocolsViewModel : ViewModelBase
{
    public LocalizationService Loc => LocalizationService.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedCategoryIndex = 0; // 0 = All, 1 = Classic, 2 = Modern Proxy, 3 = Additional, 4 = Transports

    [ObservableProperty]
    private VpnProtocolInfo? _selectedProtocol;

    public ObservableCollection<VpnProtocolInfo> FilteredProtocols { get; } = new();

    private readonly List<VpnProtocolInfo> _allProtocols;

    public ProtocolsViewModel()
    {
        _allProtocols = VpnProtocolInfo.GetAllSupportedProtocols();
        ApplyFilter();

        Loc.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(string.Empty);
            ApplyFilter();
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void SelectCategory(int index)
    {
        SelectedCategoryIndex = index;
    }

    [RelayCommand]
    public void SelectProtocol(VpnProtocolInfo proto)
    {
        SelectedProtocol = proto;
    }

    private void ApplyFilter()
    {
        FilteredProtocols.Clear();
        var query = _allProtocols.AsEnumerable();

        if (SelectedCategoryIndex == 1)
            query = query.Where(p => p.Category == ProtocolCategory.ClassicVpn);
        else if (SelectedCategoryIndex == 2)
            query = query.Where(p => p.Category == ProtocolCategory.ModernProxy);
        else if (SelectedCategoryIndex == 3)
            query = query.Where(p => p.Category == ProtocolCategory.AdditionalTech);
        else if (SelectedCategoryIndex == 4)
            query = query.Where(p => p.Category == ProtocolCategory.TransportAndCore);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string s = SearchText.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLowerInvariant().Contains(s) ||
                                     p.ShortName.ToLowerInvariant().Contains(s) ||
                                     p.DescriptionEn.ToLowerInvariant().Contains(s) ||
                                     p.DescriptionRu.ToLowerInvariant().Contains(s));
        }

        foreach (var p in query)
        {
            FilteredProtocols.Add(p);
        }

        if (SelectedProtocol == null || !FilteredProtocols.Contains(SelectedProtocol))
        {
            SelectedProtocol = FilteredProtocols.FirstOrDefault();
        }
    }
}
