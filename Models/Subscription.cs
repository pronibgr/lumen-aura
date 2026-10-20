using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Models;

public partial class Subscription : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = "Global Premium Subscription";

    [ObservableProperty]
    private string _url = "https://example.com/subscription";

    [ObservableProperty]
    private bool _isPinned = false;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isUpdating = false;

    [ObservableProperty]
    private bool _isPingingAll = false;

    [ObservableProperty]
    private string _trafficInfo = "42.8 GB / 250 GB";

    [ObservableProperty]
    private double _trafficPercent = 17.1;

    [ObservableProperty]
    private string _expireDate = "2026-12-31";

    [ObservableProperty]
    private string _expireDateText = "Истекает: 08.11.2026";

    [ObservableProperty]
    private string _autoUpdateInfo = "18.10.2026 20:49 | Автообновление - 1ч.";

    [ObservableProperty]
    private string _descriptionText = string.Empty;

    [ObservableProperty]
    private bool _hasDescription = false;

    [ObservableProperty]
    private string _websiteUrl = string.Empty;

    [ObservableProperty]
    private string _telegramUrl = string.Empty;

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.Now;

    public ObservableCollection<ServerNode> Servers { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public ObservableCollection<ServerNode> FilteredServers { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public int ServerCount => Servers.Count;

    partial void OnDescriptionTextChanged(string value)
    {
        HasDescription = !string.IsNullOrWhiteSpace(value);
    }

    public void NotifyServersChanged()
    {
        OnPropertyChanged(nameof(ServerCount));
        ApplyFilter(string.Empty);
    }

    public void ApplyFilter(string query)
    {
        FilteredServers.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var s in Servers)
            {
                FilteredServers.Add(s);
            }
        }
        else
        {
            var q = query.Trim();
            foreach (var s in Servers)
            {
                if (s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.Country.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.CountryCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.Protocol.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.TransportSecurity.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.Host.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredServers.Add(s);
                }
            }
        }
    }
}
