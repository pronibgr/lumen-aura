using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using aura.Models;

namespace aura.Services;

public interface IVpnPlatformAdapter
{
    int GetActiveNetworkMark();
    Task<bool> StartVpnAsync(ServerNode node, int httpPort, int socksPort, string configPath);
    void StopVpn();
    (long recvBytes, long sentBytes) GetNetworkStats();
}

public partial class VpnEngineService : ObservableObject
{
    private static VpnEngineService? _instance;
    public static VpnEngineService Instance => _instance ??= new VpnEngineService();

    public IVpnPlatformAdapter? PlatformAdapter { get; set; }

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private bool _isConnecting = false;

    [ObservableProperty]
    private string _statusText = "disconnected";

    [ObservableProperty]
    private string _connectionDuration = "00:00:00";

    [ObservableProperty]
    private string _downloadSpeed = "0,0 KB/S";

    [ObservableProperty]
    private string _uploadSpeed = "0,0 KB/S";

    [ObservableProperty]
    private string _totalTransferred = "0,0 MB";

    [ObservableProperty]
    private ServerNode? _activeServer;

    [ObservableProperty]
    private int _activeHttpPort = 20809;

    [ObservableProperty]
    private int _activeSocksPort = 20808;

    private readonly Timer _statsTimer;
    private Stopwatch? _connectionStopwatch;
    private Process? _coreProcess;
    private long _lastBytesReceived = 0;
    private long _lastBytesSent = 0;
    private long _accumulatedBytes = 0;

    public VpnEngineService()
    {
        _statsTimer = new Timer(1000);
        _statsTimer.Elapsed += OnStatsTimerElapsed;
        _statsTimer.AutoReset = true;
    }

    public async Task<bool> ConnectAsync(ServerNode? node)
    {
        if (node == null)
        {
            node = ActiveServer;
        }

        if (node == null)
        {
            var firstSub = SubscriptionService.Instance.JsonConfigurations.FirstOrDefault()
                        ?? SubscriptionService.Instance.Subscriptions.FirstOrDefault();
            node = firstSub?.Servers.FirstOrDefault();
        }

        if (node == null)
        {
            LogService.Instance.Log("core", "WARN", "No server node selected for connection.");
            return false;
        }

        if (IsConnected || IsConnecting) return false;

        ActiveServer = node;
        IsConnecting = true;
        StatusText = LocalizationService.Instance["StatusConnecting"];

        try
        {
            LogService.Instance.Log("core", "DIAL", $"Initiating connection to {node.Name} [{node.Host}:{node.Port}] via {node.Protocol}");

            // 1. Determine available free proxy ports (avoids conflict with Happ or system daemons)
            var (httpPort, socksPort) = GetAvailableProxyPorts();
            ActiveHttpPort = httpPort;
            ActiveSocksPort = socksPort;

            // 2. Prepare configuration file (Supports Xray JSON, WireGuard, and AmneziaWG 2.0 / 3.0, and URIs)
            int fwmark = PlatformAdapter?.GetActiveNetworkMark() ?? 0;
            string configPath = PrepareConfigFile(node, httpPort, socksPort, fwmark);

            // Check if platform adapter is installed (e.g. Android VpnService)
            if (PlatformAdapter != null)
            {
                bool started = await PlatformAdapter.StartVpnAsync(node, httpPort, socksPort, configPath);
                if (!started)
                {
                    Disconnect();
                    return false;
                }

                IsConnecting = false;
                IsConnected = true;
                StatusText = LocalizationService.Instance["StatusConnected"];
                node.IsConnected = true;

                var (androidRecv, androidSent) = PlatformAdapter.GetNetworkStats();
                _lastBytesReceived = androidRecv;
                _lastBytesSent = androidSent;
                _accumulatedBytes = 0;

                _connectionStopwatch = Stopwatch.StartNew();
                _statsTimer.Start();

                LogService.Instance.Log("core", "READY", $"Android VPN connection established to {node.Name}.");
                return true;
            }

            // 3. Locate Xray Core (Desktop)
            string? xrayPath = FindXrayBinary();
            if (xrayPath != null && File.Exists(configPath))
            {
                KillRunningCore();

                var psi = new ProcessStartInfo
                {
                    FileName = xrayPath,
                    Arguments = $"run -c \"{configPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string assetDir = FindAssetDir();
                psi.EnvironmentVariables["XRAY_LOCATION_ASSET"] = assetDir;

                _coreProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _coreProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("core", "XRAY", e.Data.Trim());
                    }
                };
                _coreProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("core", "XRAY", e.Data.Trim());
                    }
                };

                _coreProcess.Start();
                _coreProcess.BeginOutputReadLine();
                _coreProcess.BeginErrorReadLine();

                LogService.Instance.Log("core", "START", $"Core started (PID: {_coreProcess.Id}) for {node.Protocol} on HTTP:{httpPort} SOCKS:{socksPort}");

                // Check for immediate crash/exit (e.g. port collision or bad config)
                await Task.Delay(450);
                if (_coreProcess.HasExited)
                {
                    int code = _coreProcess.ExitCode;
                    LogService.Instance.Log("core", "ERROR", $"Core process terminated immediately (ExitCode: {code})");
                    ToastService.Instance.Show(LocalizationService.Instance.CurrentLanguage == "ru"
                        ? $"Ошибка запуска ядра (код {code})"
                        : $"Core process exited with code {code}");
                    Disconnect();
                    return false;
                }
            }
            else
            {
                LogService.Instance.Log("core", "INFO", "Core binary ready. Starting tunnel service.");
            }

            // 4. Enable System Proxy with the actual running ports
            SystemProxyService.Instance.EnableProxy(httpPort, socksPort);
            LogService.Instance.Log("tunnel", "SYS-PROXY", $"System proxy active on 127.0.0.1:{httpPort} (HTTP) and 127.0.0.1:{socksPort} (SOCKS5)");

            await Task.Delay(300);

            IsConnecting = false;
            IsConnected = true;
            StatusText = LocalizationService.Instance["StatusConnected"];
            node.IsConnected = true;

            var (initRecv, initSent) = GetTotalInterfaceBytes();
            _lastBytesReceived = initRecv;
            _lastBytesSent = initSent;
            _accumulatedBytes = 0;

            _connectionStopwatch = Stopwatch.StartNew();
            _statsTimer.Start();

            LogService.Instance.Log("core", "READY", $"Connection established to {node.Name}. Traffic is now routed through VPN.");
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("core", "ERROR", $"Connection failure: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    public async Task SwitchServerAsync(ServerNode node)
    {
        if (node == null) return;

        bool wasActive = IsConnected || IsConnecting;
        ActiveServer = node;

        if (wasActive)
        {
            LogService.Instance.Log("core", "SWITCH", $"Hot-switching server to '{node.Name}'");

            // 1. Disconnect and reset state so user sees 'disconnected'
            Disconnect();

            // 2. Delay so the transition from connected -> disconnected -> connecting -> connected is clearly visible
            await Task.Delay(400);

            // 3. Connect to the newly selected server with fresh counters
            await ConnectAsync(node);
        }
    }

    public async Task ReloadRoutingAsync()
    {
        if (!IsConnected || ActiveServer == null) return;

        try
        {
            LogService.Instance.Log("core", "ROUTING", "Updating tunnel routing rules dynamically...");
            int httpPort = ActiveHttpPort > 0 ? ActiveHttpPort : 20809;
            int socksPort = ActiveSocksPort > 0 ? ActiveSocksPort : 20808;

            int fwmark = PlatformAdapter?.GetActiveNetworkMark() ?? 0;
            string configPath = PrepareConfigFile(ActiveServer, httpPort, socksPort, fwmark);
            string? xrayPath = FindXrayBinary();
            if (xrayPath != null && File.Exists(configPath))
            {
                KillRunningCore();

                var psi = new ProcessStartInfo
                {
                    FileName = xrayPath,
                    Arguments = $"run -c \"{configPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string assetDir = FindAssetDir();
                psi.EnvironmentVariables["XRAY_LOCATION_ASSET"] = assetDir;

                _coreProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _coreProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("core", "XRAY", e.Data.Trim());
                    }
                };
                _coreProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("core", "XRAY", e.Data.Trim());
                    }
                };

                _coreProcess.Start();
                _coreProcess.BeginOutputReadLine();
                _coreProcess.BeginErrorReadLine();

                LogService.Instance.Log("core", "ROUTING", "Routing rules updated and core reloaded.");
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("core", "ERROR", $"Failed to reload routing: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        if (!IsConnected && !IsConnecting) return;

        if (PlatformAdapter != null)
        {
            PlatformAdapter.StopVpn();
        }
        else
        {
            KillRunningCore();
            SystemProxyService.Instance.DisableProxy();
            LogService.Instance.Log("tunnel", "SYS-PROXY", "System proxy disabled.");
        }

        _statsTimer.Stop();
        _connectionStopwatch?.Stop();

        if (ActiveServer != null)
        {
            ActiveServer.IsConnected = false;
        }

        foreach (var sub in SubscriptionService.Instance.Subscriptions.Concat(SubscriptionService.Instance.JsonConfigurations))
        {
            foreach (var s in sub.Servers)
            {
                s.IsConnected = false;
            }
        }

        IsConnecting = false;
        IsConnected = false;
        StatusText = LocalizationService.Instance["StatusDisconnected"];
        ConnectionDuration = "00:00:00";
        DownloadSpeed = "0,0 KB/S";
        UploadSpeed = "0,0 KB/S";
        TotalTransferred = "0,0 MB";
        _accumulatedBytes = 0;
        _lastBytesReceived = 0;
        _lastBytesSent = 0;

        LogService.Instance.Log("core", "STOP", "Tunnel terminated. Routing restored.");
    }

    private void KillRunningCore()
    {
        try
        {
            if (_coreProcess != null && !_coreProcess.HasExited)
            {
                _coreProcess.Kill(true);
                _coreProcess.Dispose();
                _coreProcess = null;
            }
        }
        catch { }
    }

    private static bool IsTcpPortAvailable(int port)
    {
        try
        {
            var activeTcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return !activeTcpListeners.Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public static (int httpPort, int socksPort) GetAvailableProxyPorts()
    {
        int[] httpCandidates = { 20809, 10809, 21809, 30809 };
        int[] socksCandidates = { 20808, 10808, 21808, 30808 };

        for (int i = 0; i < httpCandidates.Length; i++)
        {
            int h = httpCandidates[i];
            int s = socksCandidates[i];
            if (IsTcpPortAvailable(h) && IsTcpPortAvailable(s))
            {
                return (h, s);
            }
        }

        return (20809, 20808);
    }

    private static void SetHeaderField(JsonObject obj, string key, string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return;
        var parts = val.Split('-');
        if (uint.TryParse(parts[0].Trim(), out uint num))
        {
            obj[key] = num;
        }
        else
        {
            obj[key] = val;
        }
    }

    private static string ResolveEndpointToIp(string endpoint)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return endpoint;
            int lastColon = endpoint.LastIndexOf(':');
            if (lastColon <= 0 || lastColon >= endpoint.Length - 1) return endpoint;

            string host = endpoint.Substring(0, lastColon).Trim('[', ']');
            string port = endpoint.Substring(lastColon + 1);

            if (System.Net.IPAddress.TryParse(host, out _))
            {
                return endpoint;
            }

            var addresses = System.Net.Dns.GetHostAddresses(host);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 != null)
            {
                return $"{ipv4}:{port}";
            }
            if (addresses.Length > 0)
            {
                return $"[{addresses[0]}]:{port}";
            }
        }
        catch { }

        return endpoint;
    }

    private static string ResolveHostToIp(string host)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) return host;
            string cleanHost = host.Trim('[', ']');
            if (System.Net.IPAddress.TryParse(cleanHost, out _))
            {
                return cleanHost;
            }

            var addresses = System.Net.Dns.GetHostAddresses(cleanHost);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 != null)
            {
                return ipv4.ToString();
            }
            if (addresses.Length > 0)
            {
                return addresses[0].ToString();
            }
        }
        catch { }

        return host;
    }

    private static JsonObject? BuildOutboundFromUri(string uriString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uriString)) return null;

            if (uriString.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = uriString.Substring("vmess://".Length).Trim();
                var json = SubscriptionParser.DecodeIfBase64(base64);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string host = root.TryGetProperty("add", out var addProp) ? addProp.GetString() ?? "" : "";
                int port = 443;
                if (root.TryGetProperty("port", out var ptProp))
                {
                    if (ptProp.ValueKind == JsonValueKind.Number) port = ptProp.GetInt32();
                    else if (int.TryParse(ptProp.GetString(), out int p)) port = p;
                }
                string id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                int aid = root.TryGetProperty("aid", out var aidProp) && aidProp.ValueKind == JsonValueKind.Number ? aidProp.GetInt32() : 0;
                string net = root.TryGetProperty("net", out var netProp) ? netProp.GetString() ?? "tcp" : "tcp";
                string tls = root.TryGetProperty("tls", out var tlsProp) ? tlsProp.GetString() ?? "none" : "none";
                string sni = root.TryGetProperty("sni", out var sniProp) ? sniProp.GetString() ?? host : host;
                string path = root.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";

                var vmessUser = new JsonObject
                {
                    ["id"] = id,
                    ["alterId"] = aid,
                    ["security"] = "auto"
                };

                string resolvedHost = ResolveHostToIp(host);
                var vmessOutbound = new JsonObject
                {
                    ["protocol"] = "vmess",
                    ["tag"] = "proxy",
                    ["settings"] = new JsonObject
                    {
                        ["vnext"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["address"] = resolvedHost,
                                ["port"] = port,
                                ["users"] = new JsonArray(vmessUser)
                            }
                        }
                    }
                };

                var streamSettings = new JsonObject
                {
                    ["network"] = net.ToLowerInvariant(),
                    ["security"] = tls.ToLowerInvariant()
                };
                if (tls.Equals("tls", StringComparison.OrdinalIgnoreCase))
                {
                    streamSettings["tlsSettings"] = new JsonObject
                    {
                        ["serverName"] = sni,
                        ["allowInsecure"] = false
                    };
                }
                if (net.Equals("ws", StringComparison.OrdinalIgnoreCase))
                {
                    var ws = new JsonObject();
                    if (!string.IsNullOrEmpty(path)) ws["path"] = path;
                    if (!string.IsNullOrEmpty(sni)) ws["headers"] = new JsonObject { ["Host"] = sni };
                    streamSettings["wsSettings"] = ws;
                }
                vmessOutbound["streamSettings"] = streamSettings;
                return vmessOutbound;
            }

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                return null;

            string scheme = uri.Scheme.ToLowerInvariant();
            string hostVal = uri.Host;
            int portVal = uri.Port > 0 ? uri.Port : 443;
            var q = HttpUtility.ParseQueryString(uri.Query);

            if (scheme == "vless")
            {
                string uuid = uri.UserInfo;
                string type = q["type"] ?? q["net"] ?? "tcp";
                string security = q["security"] ?? "none";
                string pbk = q["pbk"] ?? "";
                string sni = q["sni"] ?? q["serverName"] ?? hostVal;
                string sid = q["sid"] ?? "";
                string fp = q["fp"] ?? "chrome";
                string flow = q["flow"] ?? "";
                string path = q["path"] ?? "";

                var userObj = new JsonObject
                {
                    ["id"] = uuid,
                    ["encryption"] = "none"
                };
                if (!string.IsNullOrEmpty(flow))
                {
                    userObj["flow"] = flow;
                }

                string resolvedHost = ResolveHostToIp(hostVal);
                var outbound = new JsonObject
                {
                    ["protocol"] = "vless",
                    ["tag"] = "proxy",
                    ["settings"] = new JsonObject
                    {
                        ["vnext"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["address"] = resolvedHost,
                                ["port"] = portVal,
                                ["users"] = new JsonArray(userObj)
                            }
                        }
                    }
                };

                var streamSettings = new JsonObject
                {
                    ["network"] = type.ToLowerInvariant(),
                    ["security"] = security.ToLowerInvariant()
                };

                if (security.Equals("reality", StringComparison.OrdinalIgnoreCase))
                {
                    streamSettings["realitySettings"] = new JsonObject
                    {
                        ["show"] = false,
                        ["publicKey"] = pbk,
                        ["serverName"] = sni,
                        ["shortId"] = sid,
                        ["fingerprint"] = string.IsNullOrEmpty(fp) ? "chrome" : fp
                    };
                }
                else if (security.Equals("tls", StringComparison.OrdinalIgnoreCase))
                {
                    streamSettings["tlsSettings"] = new JsonObject
                    {
                        ["serverName"] = sni,
                        ["allowInsecure"] = false
                    };
                }

                if (type.Equals("ws", StringComparison.OrdinalIgnoreCase))
                {
                    var ws = new JsonObject();
                    if (!string.IsNullOrEmpty(path)) ws["path"] = path;
                    var hostHdr = q["host"] ?? sni;
                    if (!string.IsNullOrEmpty(hostHdr)) ws["headers"] = new JsonObject { ["Host"] = hostHdr };
                    streamSettings["wsSettings"] = ws;
                }
                else if (type.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                {
                    var serviceName = q["serviceName"] ?? "";
                    streamSettings["grpcSettings"] = new JsonObject { ["serviceName"] = serviceName };
                }

                outbound["streamSettings"] = streamSettings;
                return outbound;
            }

            if (scheme == "trojan")
            {
                string password = uri.UserInfo;
                string sni = q["sni"] ?? hostVal;

                string resolvedHost = ResolveHostToIp(hostVal);
                var outbound = new JsonObject
                {
                    ["protocol"] = "trojan",
                    ["tag"] = "proxy",
                    ["settings"] = new JsonObject
                    {
                        ["servers"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["address"] = resolvedHost,
                                ["port"] = portVal,
                                ["password"] = password
                            }
                        }
                    },
                    ["streamSettings"] = new JsonObject
                    {
                        ["security"] = "tls",
                        ["tlsSettings"] = new JsonObject
                        {
                            ["serverName"] = sni,
                            ["allowInsecure"] = false
                        }
                    }
                };
                return outbound;
            }

            if (scheme == "ss")
            {
                string method = "aes-256-gcm";
                string password = "";
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userInfo = uri.UserInfo;
                    if (userInfo.Contains(':'))
                    {
                        var parts = userInfo.Split(':', 2);
                        method = parts[0];
                        password = parts[1];
                    }
                    else
                    {
                        var decoded = SubscriptionParser.DecodeIfBase64(userInfo);
                        if (decoded.Contains(':'))
                        {
                            var parts = decoded.Split(':', 2);
                            method = parts[0];
                            password = parts[1];
                        }
                    }
                }

                string resolvedHost = ResolveHostToIp(hostVal);
                var outbound = new JsonObject
                {
                    ["protocol"] = "shadowsocks",
                    ["tag"] = "proxy",
                    ["settings"] = new JsonObject
                    {
                        ["servers"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["address"] = resolvedHost,
                                ["port"] = portVal,
                                ["method"] = method,
                                ["password"] = password,
                                ["uot"] = true
                            }
                        }
                    }
                };
                return outbound;
            }
        }
        catch { }

        return null;
    }

    private static JsonObject BuildRoutingRules(int routingMode, List<ProcessItem>? selectedApps, bool bypassLan = true)
    {
        var rules = new JsonArray();

        // 1. DNS rule (TOP PRIORITY): intercept port 53 DNS queries from client inbounds and resolve via internal dns-out
        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["inboundTag"] = new JsonArray("socks", "http"),
            ["port"] = "53",
            ["outboundTag"] = "dns-out"
        });

        // 2. Reject / Blackhole DNS-over-TLS (port 853) so Android's opportunistic DoT probe immediately falls back to port 53 without delay
        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["port"] = "853",
            ["outboundTag"] = "block"
        });

        // 3. Bypass LAN rule (direct)
        if (bypassLan)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["ip"] = new JsonArray(
                    "10.0.0.0/8",
                    "172.16.0.0/12",
                    "192.168.0.0/16",
                    "127.0.0.0/8",
                    "fc00::/7",
                    "fe80::/10"
                ),
                ["outboundTag"] = "direct"
            });
        }

        // 4. Split tunneling per-application rules (Desktop only - on Android, OS VpnService handles per-app split)
        var expandedProcesses = GetExpandedProcessList(selectedApps);
        bool isAndroid = OperatingSystem.IsAndroid() || Instance.PlatformAdapter != null;

        if (routingMode == 1) // Bypass selected applications (Всё через VPN кроме выбранных)
        {
            if (expandedProcesses.Count > 0 && !isAndroid)
            {
                var procArray = new JsonArray();
                foreach (var p in expandedProcesses) procArray.Add(p);

                rules.Add(new JsonObject
                {
                    ["type"] = "field",
                    ["process"] = procArray,
                    ["outboundTag"] = "direct"
                });
            }

            // Fallback: all other traffic routes through proxy
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["network"] = "tcp,udp",
                ["outboundTag"] = "proxy"
            });
        }
        else if (routingMode == 2) // Route ONLY selected applications (Только выбранные приложения)
        {
            if (isAndroid)
            {
                // On Android: per-app whitelist is enforced by OS VpnService.Builder.AddAllowedApplication.
                // Any traffic that reaches the tunnel is already allowed, so route it to proxy.
                rules.Add(new JsonObject
                {
                    ["type"] = "field",
                    ["network"] = "tcp,udp",
                    ["outboundTag"] = "proxy"
                });
            }
            else
            {
                if (expandedProcesses.Count > 0)
                {
                    var procArray = new JsonArray();
                    foreach (var p in expandedProcesses) procArray.Add(p);

                    rules.Add(new JsonObject
                    {
                        ["type"] = "field",
                        ["process"] = procArray,
                        ["outboundTag"] = "proxy"
                    });
                }

                // Fallback: all other traffic goes direct
                rules.Add(new JsonObject
                {
                    ["type"] = "field",
                    ["network"] = "tcp,udp",
                    ["outboundTag"] = "direct"
                });
            }
        }
        else // Mode 0: Route ALL traffic through proxy (Весь трафик через VPN)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["network"] = "tcp,udp",
                ["outboundTag"] = "proxy"
            });
        }

        return new JsonObject
        {
            ["domainStrategy"] = "AsIs",
            ["rules"] = rules
        };
    }

    private static List<string> GetExpandedProcessList(List<ProcessItem>? selectedApps)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (selectedApps == null || selectedApps.Count == 0) return set.ToList();

        bool containsTerminal = false;

        foreach (var app in selectedApps)
        {
            if (string.IsNullOrWhiteSpace(app.Name)) continue;
            string name = app.Name.Trim().ToLowerInvariant();

            set.Add(name);
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                set.Add($"{name}.exe");
            }
            else
            {
                set.Add(name.Substring(0, name.Length - 4));
            }

            if (!string.IsNullOrWhiteSpace(app.Path))
            {
                try
                {
                    string filename = Path.GetFileName(app.Path).ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(filename))
                    {
                        set.Add(filename);
                        if (!filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            set.Add($"{filename}.exe");
                    }
                }
                catch { }
            }

            if (name.Contains("konsole") || name.Contains("terminal") || name.Contains("kitty") ||
                name.Contains("alacritty") || name.Contains("xterm") || name.Contains("foot") ||
                name.Contains("wezterm") || name.Contains("terminator") || name.Contains("tilix") ||
                name.Contains("cmd") || name.Contains("powershell") || name.Contains("pwsh") ||
                name.Contains("wt") || name.Contains("bash") || name.Contains("zsh") || name.Contains("fish"))
            {
                containsTerminal = true;
            }
        }

        if (containsTerminal)
        {
            string[] cliTools = new[]
            {
                "curl", "curl.exe",
                "wget", "wget.exe",
                "git", "git.exe",
                "python", "python.exe", "python3", "python3.exe",
                "bash", "bash.exe",
                "zsh", "sh", "fish",
                "node", "node.exe",
                "npm", "npm.cmd",
                "aria2c", "aria2c.exe",
                "ssh", "ssh.exe",
                "pwsh", "pwsh.exe",
                "powershell", "powershell.exe",
                "cmd", "cmd.exe"
            };
            foreach (var t in cliTools)
            {
                set.Add(t);
            }
        }

        return set.ToList();
    }

    private static void ApplySockoptMark(JsonObject outbound, int mark)
    {
        if (mark <= 0) return;
        if (outbound["streamSettings"] is not JsonObject streamSettings)
        {
            streamSettings = new JsonObject();
            outbound["streamSettings"] = streamSettings;
        }
        if (streamSettings["sockopt"] is not JsonObject sockopt)
        {
            sockopt = new JsonObject();
            streamSettings["sockopt"] = sockopt;
        }
        sockopt["mark"] = mark;
    }

    private static string PrepareConfigFile(ServerNode node, int httpPort, int socksPort, int fwmark = 0)
    {
        string tmpPath = Path.Combine(Path.GetTempPath(), "aura_xray_config.json");
        var settings = SettingsService.Instance.GetSettings();
        var routingRulesObj = BuildRoutingRules(settings.RoutingMode, settings.SelectedApplications, settings.BypassLanForSystemProxy);

        var defaultInbounds = new JsonArray
        {
            new JsonObject
            {
                ["port"] = socksPort,
                ["protocol"] = "socks",
                ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = true },
                ["sniffing"] = new JsonObject { ["enabled"] = true, ["destOverride"] = new JsonArray("http", "tls", "quic", "fakedns") },
                ["tag"] = "socks"
            },
            new JsonObject
            {
                ["port"] = httpPort,
                ["protocol"] = "http",
                ["settings"] = new JsonObject { ["allowTransparent"] = false },
                ["sniffing"] = new JsonObject { ["enabled"] = true, ["destOverride"] = new JsonArray("http", "tls", "quic", "fakedns") },
                ["tag"] = "http"
            }
        };

        var defaultFakeDns = new JsonArray
        {
            new JsonObject
            {
                ["ipPool"] = "198.18.0.0/15",
                ["poolSize"] = 65535
            }
        };

        var defaultDns = new JsonObject
        {
            ["queryStrategy"] = "UseIPv4",
            ["servers"] = new JsonArray(
                "fakedns",
                "tcp://1.1.1.1",
                "tcp://8.8.8.8",
                "tcp://77.88.8.8",
                "1.1.1.1",
                "8.8.8.8"
            )
        };

        // Case 1: WireGuard or AmneziaWG (.conf file or AwgConfig)
        var awg = node.AwgConfig;
        if (awg == null && !string.IsNullOrWhiteSpace(node.RawConfig) && node.RawConfig.Contains("[Interface]"))
        {
            awg = AmneziaWgConfig.ParseIni(node.RawConfig);
        }

        if (awg != null && !string.IsNullOrWhiteSpace(awg.PrivateKey))
        {
            string resolvedEndpoint = ResolveEndpointToIp(awg.Endpoint);
            var peerObj = new JsonObject
            {
                ["publicKey"] = awg.PublicKey,
                ["endpoint"] = resolvedEndpoint,
                ["keepAlive"] = awg.PersistentKeepalive > 0 ? awg.PersistentKeepalive : 25
            };
            if (!string.IsNullOrWhiteSpace(awg.PresharedKey))
            {
                peerObj["preSharedKey"] = awg.PresharedKey;
            }

            var addrArray = new JsonArray();
            if (!string.IsNullOrWhiteSpace(awg.Address))
            {
                foreach (var part in awg.Address.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        addrArray.Add(trimmed);
                }
            }
            if (addrArray.Count == 0) addrArray.Add("10.0.0.2/32");

            var wgSettings = new JsonObject
            {
                ["secretKey"] = awg.PrivateKey,
                ["address"] = addrArray,
                ["peers"] = new JsonArray(peerObj),
                ["mtu"] = awg.Mtu > 0 ? awg.Mtu : 1280
            };

            // AmneziaWG Obfuscation Fields (Supported by Xray 26.7.28 gVisor TUN)
            if (awg.Jc > 0) wgSettings["jc"] = awg.Jc;
            if (awg.Jmin > 0) wgSettings["jmin"] = awg.Jmin;
            if (awg.Jmax > 0) wgSettings["jmax"] = awg.Jmax;
            if (awg.S1 > 0) wgSettings["s1"] = awg.S1;
            if (awg.S2 > 0) wgSettings["s2"] = awg.S2;
            if (awg.S3 > 0) wgSettings["s3"] = awg.S3;
            if (awg.S4 > 0) wgSettings["s4"] = awg.S4;
            SetHeaderField(wgSettings, "h1", awg.H1);
            SetHeaderField(wgSettings, "h2", awg.H2);
            SetHeaderField(wgSettings, "h3", awg.H3);
            SetHeaderField(wgSettings, "h4", awg.H4);

            var proxyOb = new JsonObject
            {
                ["protocol"] = "wireguard",
                ["settings"] = wgSettings,
                ["tag"] = "proxy"
            };
            ApplySockoptMark(proxyOb, fwmark);

            var directOb = new JsonObject
            {
                ["protocol"] = "freedom",
                ["tag"] = "direct"
            };
            ApplySockoptMark(directOb, fwmark);

            var wgConfig = new JsonObject
            {
                ["log"] = new JsonObject { ["loglevel"] = "warning" },
                ["fakedns"] = defaultFakeDns,
                ["dns"] = defaultDns,
                ["routing"] = routingRulesObj,
                ["inbounds"] = defaultInbounds,
                ["outbounds"] = new JsonArray
                {
                    proxyOb,
                    new JsonObject
                    {
                        ["protocol"] = "dns",
                        ["tag"] = "dns-out"
                    },
                    new JsonObject
                    {
                        ["protocol"] = "blackhole",
                        ["tag"] = "block"
                    },
                    directOb
                }
            };

            File.WriteAllText(tmpPath, wgConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return tmpPath;
        }

        // Case 2: Full Xray JSON config with outbounds (custom JSON or subscription feed)
        if (!string.IsNullOrWhiteSpace(node.RawConfig) && node.RawConfig.TrimStart().StartsWith('{') && node.RawConfig.Contains("\"outbounds\""))
        {
            try
            {
                var root = JsonNode.Parse(node.RawConfig)?.AsObject();
                if (root != null)
                {
                    // Ensure inbounds with detected free ports
                    root["inbounds"] = defaultInbounds;

                    // Reorder outbounds to place selected node at index 0 and normalize tag to "proxy"
                    if (root["outbounds"] is JsonArray outbounds)
                    {
                        JsonNode? targetOutbound = null;
                        for (int i = 0; i < outbounds.Count; i++)
                        {
                            var ob = outbounds[i];
                            var tag = ob?["tag"]?.ToString();
                            if (!string.IsNullOrEmpty(node.OutboundTag) && tag == node.OutboundTag)
                            {
                                targetOutbound = ob;
                                outbounds.RemoveAt(i);
                                break;
                            }
                            else if (tag == node.Name)
                            {
                                targetOutbound = ob;
                                outbounds.RemoveAt(i);
                                break;
                            }
                        }

                        if (targetOutbound != null)
                        {
                            outbounds.Insert(0, targetOutbound);
                        }

                        if (outbounds.Count > 0 && outbounds[0] is JsonObject primaryOb)
                        {
                            primaryOb["tag"] = "proxy";

                            // Pre-resolve proxy host to IP to break recursive DNS deadlock in Xray
                            try
                            {
                                if (primaryOb["settings"] is JsonObject obSettings)
                                {
                                    if (obSettings["vnext"] is JsonArray vnext && vnext.Count > 0 && vnext[0] is JsonObject vnext0)
                                    {
                                        string? origHost = vnext0["address"]?.ToString();
                                        if (!string.IsNullOrEmpty(origHost))
                                        {
                                            string resolvedIp = ResolveHostToIp(origHost);
                                            if (resolvedIp != origHost)
                                            {
                                                vnext0["address"] = resolvedIp;
                                                if (primaryOb["streamSettings"] is JsonObject ss)
                                                {
                                                    if (ss["realitySettings"] is JsonObject rs && string.IsNullOrEmpty(rs["serverName"]?.ToString()))
                                                    {
                                                        rs["serverName"] = origHost;
                                                    }
                                                    if (ss["tlsSettings"] is JsonObject ts && string.IsNullOrEmpty(ts["serverName"]?.ToString()))
                                                    {
                                                        ts["serverName"] = origHost;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (obSettings["servers"] is JsonArray servers && servers.Count > 0 && servers[0] is JsonObject server0)
                                    {
                                        string? origHost = server0["address"]?.ToString();
                                        if (!string.IsNullOrEmpty(origHost))
                                        {
                                            string resolvedIp = ResolveHostToIp(origHost);
                                            if (resolvedIp != origHost)
                                            {
                                                server0["address"] = resolvedIp;
                                                if (primaryOb["streamSettings"] is JsonObject ss && ss["tlsSettings"] is JsonObject ts && string.IsNullOrEmpty(ts["serverName"]?.ToString()))
                                                {
                                                    ts["serverName"] = origHost;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // Ensure dedicated dns-out outbound is present for local DNS interception
                        if (!outbounds.Any(o => o?["tag"]?.ToString() == "dns-out"))
                        {
                            outbounds.Add(new JsonObject
                            {
                                ["protocol"] = "dns",
                                ["tag"] = "dns-out"
                            });
                        }

                        // Ensure dedicated block outbound is present for DoT/unwanted port rejection
                        if (!outbounds.Any(o => o?["tag"]?.ToString() == "block"))
                        {
                            outbounds.Add(new JsonObject
                            {
                                ["protocol"] = "blackhole",
                                ["tag"] = "block"
                            });
                        }

                        // Ensure dedicated direct outbound is present
                        if (!outbounds.Any(o => o?["tag"]?.ToString() == "direct"))
                        {
                            outbounds.Add(new JsonObject
                            {
                                ["protocol"] = "freedom",
                                ["tag"] = "direct"
                            });
                        }
                    }

                    // 1. Clean up observatory & burstObservatory (their failed pings cause balancers to blackhole all traffic)
                    root.Remove("burstObservatory");
                    root.Remove("observatory");

                    // 2. Configure FakeDNS & robust DNS servers without slow/broken DoH
                    root["fakedns"] = defaultFakeDns;
                    root["dns"] = defaultDns;
                    root["log"] = new JsonObject { ["loglevel"] = "warning" };

                    // 3. Set clean, robust routing rules according to current user Settings (prevents fragile geosite crash)
                    root["routing"] = routingRulesObj;

                    // 4. Apply physical network fwmark to outbounds (bypasses Android VPN tun0 and satisfies rp_filter)
                    if (fwmark > 0 && root["outbounds"] is JsonArray allOutbounds)
                    {
                        foreach (var ob in allOutbounds)
                        {
                            if (ob is JsonObject obObj)
                            {
                                var tag = obObj["tag"]?.ToString();
                                if (tag != "dns-out" && tag != "block")
                                {
                                    ApplySockoptMark(obObj, fwmark);
                                }
                            }
                        }
                    }

                    File.WriteAllText(tmpPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    return tmpPath;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("core", "WARN", $"Error adjusting JSON config: {ex.Message}");
            }
        }

        // Case 3: Direct URI protocol (vless://, vmess://, trojan://, ss://)
        var uriOutbound = BuildOutboundFromUri(node.RawConfig);
        if (uriOutbound != null)
        {
            ApplySockoptMark(uriOutbound, fwmark);

            var directOb = new JsonObject
            {
                ["protocol"] = "freedom",
                ["tag"] = "direct"
            };
            ApplySockoptMark(directOb, fwmark);

            var uriConfig = new JsonObject
            {
                ["log"] = new JsonObject { ["loglevel"] = "warning" },
                ["fakedns"] = defaultFakeDns,
                ["dns"] = defaultDns,
                ["routing"] = routingRulesObj,
                ["inbounds"] = defaultInbounds,
                ["outbounds"] = new JsonArray
                {
                    uriOutbound,
                    new JsonObject
                    {
                        ["protocol"] = "dns",
                        ["tag"] = "dns-out"
                    },
                    new JsonObject
                    {
                        ["protocol"] = "blackhole",
                        ["tag"] = "block"
                    },
                    directOb
                }
            };

            File.WriteAllText(tmpPath, uriConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return tmpPath;
        }

        // Case 4: Fallback minimal Xray config
        var fallbackConfig = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = "warning" },
            ["routing"] = routingRulesObj,
            ["inbounds"] = defaultInbounds,
            ["outbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["protocol"] = "freedom",
                    ["tag"] = "direct"
                }
            }
        };

        File.WriteAllText(tmpPath, fallbackConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return tmpPath;
    }

    private static string? FindXrayBinary()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var winCandidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "xray.exe"),
                Path.Combine(AppContext.BaseDirectory, "bin", "xray.exe"),
                Path.Combine(AppContext.BaseDirectory, "core", "xray.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "xray", "xray.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "xray", "xray.exe"),
                "xray.exe"
            };
            foreach (var c in winCandidates)
            {
                if (File.Exists(c)) return c;
            }
            return "xray.exe";
        }
        else
        {
            var candidates = new[]
            {
                "/opt/happ/bin/core/xray",
                "/usr/local/bin/xray",
                "/usr/bin/xray",
                Path.Combine(AppContext.BaseDirectory, "xray"),
                Path.Combine(AppContext.BaseDirectory, "bin", "xray")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }
    }

    private static string FindAssetDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string baseDir = AppContext.BaseDirectory;
            if (File.Exists(Path.Combine(baseDir, "geoip.dat"))) return baseDir;
            if (Directory.Exists(Path.Combine(baseDir, "bin"))) return Path.Combine(baseDir, "bin");
            if (Directory.Exists(Path.Combine(baseDir, "core"))) return Path.Combine(baseDir, "core");
            return baseDir;
        }
        if (Directory.Exists("/opt/happ/bin/core")) return "/opt/happ/bin/core";
        if (Directory.Exists("/usr/share/xray")) return "/usr/share/xray";
        return Path.GetTempPath();
    }

    private void OnStatsTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_connectionStopwatch == null || !IsConnected) return;

        var elapsed = _connectionStopwatch.Elapsed;
        string duration = string.Format("{0:D2}:{1:D2}:{2:D2}",
            (int)elapsed.TotalHours,
            elapsed.Minutes,
            elapsed.Seconds);

        var (currentRecv, currentSent) = PlatformAdapter != null
            ? PlatformAdapter.GetNetworkStats()
            : GetTotalInterfaceBytes();

        long deltaRecv = currentRecv - _lastBytesReceived;
        long deltaSent = currentSent - _lastBytesSent;

        _lastBytesReceived = currentRecv;
        _lastBytesSent = currentSent;

        if (deltaRecv < 0) deltaRecv = 0;
        if (deltaSent < 0) deltaSent = 0;

        _accumulatedBytes += (deltaRecv + deltaSent);

        var settings = SettingsService.Instance.GetSettings();
        bool useBits = settings.SpeedUnit == "Bits";

        string dlStr;
        string ulStr;

        if (useBits)
        {
            double dlBits = deltaRecv * 8.0;
            double ulBits = deltaSent * 8.0;

            if (dlBits >= 1_000_000.0)
                dlStr = $"{dlBits / 1_000_000.0:F1} Mbps";
            else
                dlStr = $"{dlBits / 1_000.0:F1} Kbps";

            if (ulBits >= 1_000_000.0)
                ulStr = $"{ulBits / 1_000_000.0:F1} Mbps";
            else
                ulStr = $"{ulBits / 1_000.0:F1} Kbps";
        }
        else
        {
            if (deltaRecv > 1024 * 1024)
                dlStr = $"{deltaRecv / (1024.0 * 1024.0):F1} MB/S";
            else
                dlStr = $"{deltaRecv / 1024.0:F1} KB/S";

            if (deltaSent > 1024 * 1024)
                ulStr = $"{deltaSent / (1024.0 * 1024.0):F1} MB/S";
            else
                ulStr = $"{deltaSent / 1024.0:F1} KB/S";
        }

        string totalStr;
        if (_accumulatedBytes > 1024 * 1024 * 1024)
            totalStr = $"{_accumulatedBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        else
            totalStr = $"{_accumulatedBytes / (1024.0 * 1024.0):F1} MB";

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ConnectionDuration = duration;
            DownloadSpeed = dlStr;
            UploadSpeed = ulStr;
            TotalTransferred = totalStr;
        });
    }

    private static (long recv, long sent) GetTotalInterfaceBytes()
    {
        long totalRecv = 0;
        long totalSent = 0;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var stats = ni.GetIPv4Statistics();
                    totalRecv += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                }
            }
        }
        catch
        {
            // fallback
        }

        return (totalRecv, totalSent);
    }
}
