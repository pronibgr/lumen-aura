using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Models;

public partial class AppSettings : ObservableObject
{
    // General
    [ObservableProperty]
    private string _language = "en"; // "en" (default) or "ru"

    [ObservableProperty]
    private string _speedUnit = "Bytes"; // "Bytes" or "Bits"

    [ObservableProperty]
    private string _connectionUserAgent = "aura/1.0";

    // Routing
    // 0: Route all traffic through VPN
    // 1: Bypass selected applications
    // 2: Route only selected applications
    [ObservableProperty]
    private int _routingMode = 0;

    public List<ProcessItem> SelectedApplications { get; set; } = new();

    // Fragmentation
    [ObservableProperty]
    private bool _enableFragmentation = false;

    [ObservableProperty]
    private string _fragLength = "100-200";

    [ObservableProperty]
    private string _fragInterval = "10-20";

    [ObservableProperty]
    private string _fragPackets = "tlshello";

    // Multiplexer (MUX)
    [ObservableProperty]
    private bool _enableMultiplexer = false;

    [ObservableProperty]
    private int _muxConcurrency = 8;

    [ObservableProperty]
    private string _muxProtocol = "h2mux";

    // Advanced Settings (from screenshot)
    [ObservableProperty]
    private bool _useLocalDns = false;

    [ObservableProperty]
    private bool _dnsLeakProtection = true;

    [ObservableProperty]
    private bool _packetSniffing = true; // Анализ пакетов

    [ObservableProperty]
    private bool _autoStart = false; // Автозапуск

    [ObservableProperty]
    private bool _systemProxy = true; // Системный прокси

    [ObservableProperty]
    private bool _bypassLanForSystemProxy = true; // Не использовать системный прокси для локальных адресов

    [ObservableProperty]
    private bool _tunEnabled = true; // TUN

    [ObservableProperty]
    private string _tunProvider = "Xray TUN"; // Провайдер TUN

    [ObservableProperty]
    private bool _enableDnsForTun = true; // Включить DNS для TUN

    [ObservableProperty]
    private bool _allowLanConnections = false; // Разрешить подключения из LAN

    [ObservableProperty]
    private string _currentLanIp = "127.0.0.1";

    // Subscriptions Settings
    [ObservableProperty]
    private bool _autoUpdateSubscriptions = true;

    [ObservableProperty]
    private int _updateIntervalHours = 12;

    [ObservableProperty]
    private bool _updateNotifications = true;

    [ObservableProperty]
    private int _requestTimeoutSeconds = 15;

    [ObservableProperty]
    private bool _updateOnLaunch = true;

    [ObservableProperty]
    private bool _pingOnLaunch = true;

    [ObservableProperty]
    private string _subscriptionUserAgent = "lumen-aura/1.0";

    // Ping Settings
    // "via Proxy GET", "via Proxy HEAD", "TCP", "ICMP"
    [ObservableProperty]
    private string _pingType = "via Proxy GET";

    // "default", "double", "keepalive"
    [ObservableProperty]
    private string _viaProxyMode = "default";

    // "Number" (Число) or "Icon" (Иконка)
    [ObservableProperty]
    private string _pingDisplayMode = "Number";

    // Extra Core Settings
    [ObservableProperty]
    private bool _sandboxSafeMode = true; // Prevents interfering with existing VPN connections!

    [ObservableProperty]
    private string _customPrimaryDns = "1.1.1.1";

    [ObservableProperty]
    private string _customSecondaryDns = "8.8.8.8";

    [ObservableProperty]
    private int _inboundHttpPort = 10809;

    [ObservableProperty]
    private int _inboundSocksPort = 10808;

    [ObservableProperty]
    private int _inboundMixedPort = 2080;

    [ObservableProperty]
    private int _mtu = 1420;

    [ObservableProperty]
    private bool _killSwitch = false;

    [ObservableProperty]
    private string _themeName = "Aura Dark";
}
