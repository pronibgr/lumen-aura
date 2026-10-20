using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Models;

public partial class ServerNode : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubscriptionId { get; set; } = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _country = "Germany";

    [ObservableProperty]
    private string _countryCode = "DE";

    [ObservableProperty]
    private string _flagEmoji = "🇩🇪";

    [ObservableProperty]
    private string _transportSecurity = "VLESS / TCP / REALITY / JSON";

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 443;

    [ObservableProperty]
    private string _protocol = "AWG";

    [ObservableProperty]
    private string _badgeColor = "#8b5cf6";

    [ObservableProperty]
    private int _pingMs = -1;

    [ObservableProperty]
    private bool _pingSuccess = false;

    [ObservableProperty]
    private bool _isPinging = false;

    [ObservableProperty]
    private bool _hasPinged = false;

    [ObservableProperty]
    private bool _isSelected = false;

    [ObservableProperty]
    private bool _isConnected = false;

    public string RawConfig { get; set; } = string.Empty;
    public string OutboundTag { get; set; } = string.Empty;
    public AmneziaWgConfig? AwgConfig { get; set; }
    public string TransportInfo { get; set; } = "TCP";
    public string SecurityInfo { get; set; } = "TLS";

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? string.Empty : aura.Services.CountryFlagHelper.CleanServerName(Name);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPingBadgeVisible => HasPinged || IsPinging;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayPing
    {
        get
        {
            if (IsPinging) return "...";
            if (!HasPinged) return "...";
            if (!PingSuccess || PingMs <= 0) return "n/a";
            return $"{PingMs} ms";
        }
    }

    private static readonly IBrush GrayForeground = new SolidColorBrush(Color.Parse("#888888"));
    private static readonly IBrush GrayBackground = new SolidColorBrush(Color.FromArgb(0x28, 0x88, 0x88, 0x88));
    private static readonly IBrush RedForeground = new SolidColorBrush(Color.Parse("#ef4444"));
    private static readonly IBrush RedBackground = new SolidColorBrush(Color.FromArgb(0x28, 0xef, 0x44, 0x44));
    private static readonly IBrush GreenForeground = new SolidColorBrush(Color.Parse("#10b981"));
    private static readonly IBrush GreenBackground = new SolidColorBrush(Color.FromArgb(0x28, 0x10, 0xb9, 0x81));
    private static readonly IBrush YellowForeground = new SolidColorBrush(Color.Parse("#eab308"));
    private static readonly IBrush YellowBackground = new SolidColorBrush(Color.FromArgb(0x28, 0xea, 0xb3, 0x08));
    private static readonly IBrush OrangeForeground = new SolidColorBrush(Color.Parse("#f97316"));
    private static readonly IBrush OrangeBackground = new SolidColorBrush(Color.FromArgb(0x28, 0xf9, 0x73, 0x16));

    [System.Text.Json.Serialization.JsonIgnore]
    public IBrush PingStatusColor
    {
        get
        {
            if (IsPinging || !HasPinged) return GrayForeground;
            if (!PingSuccess || PingMs <= 0) return RedForeground;
            if (PingMs < 80) return GreenForeground;
            if (PingMs < 160) return YellowForeground;
            return OrangeForeground;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public IBrush PingBadgeBackground
    {
        get
        {
            if (IsPinging || !HasPinged) return GrayBackground;
            if (!PingSuccess || PingMs <= 0) return RedBackground;
            if (PingMs < 80) return GreenBackground;
            if (PingMs < 160) return YellowBackground;
            return OrangeBackground;
        }
    }

    public void NotifyPingUpdated()
    {
        OnPropertyChanged(nameof(IsPingBadgeVisible));
        OnPropertyChanged(nameof(DisplayPing));
        OnPropertyChanged(nameof(PingStatusColor));
        OnPropertyChanged(nameof(PingBadgeBackground));
    }
}
