using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using AndroidX.Core.App;
using System.Text.Json.Nodes;
using aura.Models;
using aura.Services;

namespace aura.Android;

[Service(
    Name = "aura.Android.AuraVpnService",
    Permission = "android.permission.BIND_VPN_SERVICE",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class AuraVpnService : VpnService
{
    public const string ActionConnect = "aura.intent.action.CONNECT";
    public const string ActionDisconnect = "aura.intent.action.DISCONNECT";
    public const string ExtraServerName = "extra_server_name";
    public const string ExtraProtocol = "extra_protocol";
    public const string ExtraHost = "extra_host";
    public const string ExtraPort = "extra_port";
    public const string ExtraSocksPort = "extra_socks_port";
    public const string ExtraHttpPort = "extra_http_port";
    public const string ExtraConfigPath = "extra_config_path";

    private const int NotificationId = 1001;
    private const string ChannelId = "aura_vpn_channel";

    private ParcelFileDescriptor? _vpnInterface;
    private CancellationTokenSource? _tunnelCts;
    private static AuraVpnService? _instance;
    private bool _isForegroundActive;

    private System.Diagnostics.Process? _xrayProcess;
    private System.Diagnostics.Process? _tun2socksProcess;

    public static AuraVpnService? Instance => _instance;
    public bool IsTunnelActive => _vpnInterface != null;

    public override void OnCreate()
    {
        base.OnCreate();
        _instance = this;
        CreateNotificationChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent == null) return StartCommandResult.NotSticky;

        string? action = intent.Action;
        if (action == ActionConnect)
        {
            string serverName = intent.GetStringExtra(ExtraServerName) ?? "Default Server";
            string protocol = intent.GetStringExtra(ExtraProtocol) ?? "VPN";
            int socksPort = intent.GetIntExtra(ExtraSocksPort, 20808);
            int httpPort = intent.GetIntExtra(ExtraHttpPort, 20809);
            string? configPath = intent.GetStringExtra(ExtraConfigPath);

            // 1. Immediately satisfy Android foreground service requirement to prevent OS timeout kill
            SafeStartForeground(serverName, protocol);

            // 2. Start TUN creation & proxy core asynchronously
            Task.Run(() => StartTunnel(serverName, protocol, socksPort, httpPort, configPath));
            return StartCommandResult.Sticky;
        }
        else if (action == ActionDisconnect)
        {
            StopTunnel();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.NotSticky;
    }

    private void SafeStartForeground(string serverName, string protocol)
    {
        try
        {
            var notification = CreateNotification(serverName, protocol);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                try
                {
                    StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
                    _isForegroundActive = true;
                    return;
                }
                catch
                {
                    // Fallback to standard start foreground
                }
            }

            StartForeground(NotificationId, notification);
            _isForegroundActive = true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "WARN", $"Could not promote to foreground service: {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
    private static extern int fcntl(int fd, int cmd, int arg);
    private const int F_GETFD = 1;
    private const int F_SETFD = 2;

    private void StartTunnel(string serverName, string protocol, int socksPort, int httpPort, string? configPath)
    {
        try
        {
            LogService.Instance.Log("android", "VPN", $"Building TUN interface for {serverName} [{protocol}]...");
            global::Android.Util.Log.Info("AuraApp", $"Building TUN interface for {serverName} [{protocol}]...");

            var builder = new Builder(this);
            builder.SetSession("lumen aura");
            builder.SetMtu(1400);

            // Virtual tunnel network configuration (RFC 1918 Private Subnet: 26.26.26.1/30)
            builder.AddAddress("26.26.26.1", 30);
            builder.AddRoute("0.0.0.0", 0);

            // DNS servers (routed via tun2socks -> Xray FakeDNS)
            builder.AddDnsServer("26.26.26.2");

            // ===================================================================
            // SYSTEM SPLIT-TUNNELING (Exact Android OS per-app rules)
            // ===================================================================
            var settings = SettingsService.Instance.GetSettings();

            if (settings.RoutingMode == 2 && settings.SelectedApplications != null && settings.SelectedApplications.Count > 0) // Route ONLY selected applications
            {
                LogService.Instance.Log("android", "ROUTING", $"Split-tunneling: Route ONLY {settings.SelectedApplications.Count} selected apps.");
                foreach (var app in settings.SelectedApplications)
                {
                    string pkg = !string.IsNullOrWhiteSpace(app.PackageName) ? app.PackageName : app.Name;
                    if (!string.IsNullOrWhiteSpace(pkg))
                    {
                        try
                        {
                            builder.AddAllowedApplication(pkg);
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Log("android", "WARN", $"Could not allow package '{pkg}': {ex.Message}");
                        }
                    }
                }
            }
            else // Mode 0 (Route ALL) or Mode 1 (Bypass selected) or Mode 2 with 0 apps selected
            {
                // Exclude lumen aura itself from VPN to avoid infinite routing loop
                try
                {
                    if (!string.IsNullOrEmpty(PackageName))
                    {
                        builder.AddDisallowedApplication(PackageName);
                    }
                }
                catch { }

                if (settings.RoutingMode == 1 && settings.SelectedApplications != null && settings.SelectedApplications.Count > 0) // Bypass selected applications
                {
                    LogService.Instance.Log("android", "ROUTING", $"Split-tunneling: Bypass {settings.SelectedApplications.Count} selected apps.");
                    foreach (var app in settings.SelectedApplications)
                    {
                        string pkg = !string.IsNullOrWhiteSpace(app.PackageName) ? app.PackageName : app.Name;
                        if (!string.IsNullOrWhiteSpace(pkg))
                        {
                            try
                            {
                                builder.AddDisallowedApplication(pkg);
                            }
                            catch (Exception ex)
                            {
                                LogService.Instance.Log("android", "WARN", $"Could not bypass package '{pkg}': {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    LogService.Instance.Log("android", "ROUTING", "Split-tunneling: Route ALL applications through VPN.");
                }
            }

            builder.SetBlocking(false);

            _vpnInterface = builder.Establish();
            if (_vpnInterface == null)
            {
                LogService.Instance.Log("android", "ERROR", "Failed to establish Android VPN interface (permission denied or OS error).");
                global::Android.Util.Log.Error("AuraApp", "Failed to establish Android VPN interface.");
                StopTunnel();
                return;
            }

            // Bind VpnService to underlying physical network (routes protected/disallowed sockets properly)
            try
            {
                var cm = (ConnectivityManager?)GetSystemService(ConnectivityService);
                var networks = cm?.GetAllNetworks();
                Network? physicalNet = null;
                if (networks != null)
                {
                    foreach (var net in networks)
                    {
                        var caps = cm?.GetNetworkCapabilities(net);
                        if (caps != null && caps.HasCapability(NetCapability.Internet) && !caps.HasTransport((TransportType)4))
                        {
                            physicalNet = net;
                            break;
                        }
                    }
                }
                if (physicalNet != null && Build.VERSION.SdkInt >= BuildVersionCodes.LollipopMr1)
                {
                    SetUnderlyingNetworks(new[] { physicalNet });
                    LogService.Instance.Log("android", "TUN", $"Set underlying physical network: {physicalNet}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("android", "WARN", $"Could not bind underlying networks: {ex.Message}");
            }

            int tunFd = _vpnInterface.Fd;

            // Clear FD_CLOEXEC on the tun file descriptor so child process tun2socks inherits it across execve
            try
            {
                int fcntlRes = fcntl(tunFd, F_SETFD, 0);
                LogService.Instance.Log("android", "TUN", $"TUN FD {tunFd} cleared FD_CLOEXEC (result: {fcntlRes})");
                global::Android.Util.Log.Info("AuraApp", $"TUN FD {tunFd} cleared FD_CLOEXEC (result: {fcntlRes})");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("android", "WARN", $"fcntl failed on TUN FD: {ex.Message}");
            }

            // Update foreground notification with connected status
            SafeStartForeground(serverName, protocol);

            // Start native Xray core and tun2socks bridge
            StartNativeEngines(socksPort, httpPort, configPath, tunFd);

            LogService.Instance.Log("android", "READY", $"Android VPN active. TUN FD: {tunFd}");
            global::Android.Util.Log.Info("AuraApp", $"Android VPN active. TUN FD: {tunFd}");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "ERROR", $"VPN Startup failure: {ex.Message}");
            global::Android.Util.Log.Error("AuraApp", $"VPN Startup failure: {ex.Message}");
            StopTunnel();
        }
    }

    private void KillStaleEngines()
    {
        try
        {
            int myPid = global::Android.OS.Process.MyPid();
            foreach (var procDir in Directory.GetDirectories("/proc"))
            {
                string pidStr = Path.GetFileName(procDir);
                if (int.TryParse(pidStr, out int pid) && pid != myPid && pid > 1)
                {
                    try
                    {
                        string cmdlinePath = Path.Combine(procDir, "cmdline");
                        if (File.Exists(cmdlinePath))
                        {
                            string cmdline = File.ReadAllText(cmdlinePath);
                            if (cmdline.Contains("libxray.so") || cmdline.Contains("libtun2socks.so"))
                            {
                                try
                                {
                                    var p = System.Diagnostics.Process.GetProcessById(pid);
                                    p.Kill();
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private int GetPhysicalNetworkNetId()
    {
        try
        {
            var cm = (ConnectivityManager?)GetSystemService(ConnectivityService);
            if (cm != null)
            {
                var networks = cm.GetAllNetworks();
                if (networks != null)
                {
                    foreach (var net in networks)
                    {
                        var caps = cm.GetNetworkCapabilities(net);
                        if (caps != null && !caps.HasTransport((TransportType)4))
                        {
                            if (int.TryParse(net.ToString(), out int parsed) && parsed > 0)
                                return parsed;
                            int id = (int)(net.NetworkHandle >> 32);
                            if (id > 0) return id;
                        }
                    }
                }
                if (cm.ActiveNetwork != null)
                {
                    if (int.TryParse(cm.ActiveNetwork.ToString(), out int actId) && actId > 0)
                        return actId;
                }
            }
        }
        catch { }
        return 100;
    }

    private void EnsureFwmarkInConfig(string? configPath)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath)) return;
        try
        {
            int netId = GetPhysicalNetworkNetId();
            string json = File.ReadAllText(configPath);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root != null && root["outbounds"] is JsonArray outbounds)
            {
                bool modified = false;
                foreach (var ob in outbounds)
                {
                    if (ob is JsonObject obObj)
                    {
                        var tag = obObj["tag"]?.ToString();
                        if (tag != "dns-out" && tag != "block")
                        {
                            if (obObj["streamSettings"] is not JsonObject ss)
                            {
                                ss = new JsonObject();
                                obObj["streamSettings"] = ss;
                            }
                            if (ss["sockopt"] is not JsonObject sockopt)
                            {
                                sockopt = new JsonObject();
                                ss["sockopt"] = sockopt;
                            }
                            sockopt["mark"] = netId;
                            modified = true;
                        }
                    }
                }
                if (modified)
                {
                    File.WriteAllText(configPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    global::Android.Util.Log.Info("AuraApp", $"[NET] Injected fwmark {netId} into Xray config {configPath}");
                }
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("AuraApp", $"[NET] Could not inject fwmark into config: {ex.Message}");
        }
    }

    private void StartNativeEngines(int socksPort, int httpPort, string? configPath, int tunFd)
    {
        // 0. Kill any leftover or orphaned native processes from previous sessions
        StopNativeEngines();
        KillStaleEngines();

        string nativeDir = ApplicationInfo?.NativeLibraryDir ?? "";
        string xrayPath = Path.Combine(nativeDir, "libxray.so");
        string tun2socksPath = Path.Combine(nativeDir, "libtun2socks.so");
        string cacheDir = ApplicationContext?.CacheDir?.AbsolutePath ?? Path.GetTempPath();

        // Ensure geoip.dat and geosite.dat assets are unpacked for Xray
        EnsureAssetsExtracted(cacheDir);

        // Ensure Xray outbound sockets have physical network fwmark to satisfy rp_filter and bypass tun0
        EnsureFwmarkInConfig(configPath);

        // 1. Start Xray-core
        if (File.Exists(xrayPath) && !string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            try
            {
                var psiXray = new ProcessStartInfo
                {
                    FileName = xrayPath,
                    Arguments = $"run -c \"{configPath}\"",
                    WorkingDirectory = cacheDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Root SSL CA certificates for Go crypto/x509 on Android
                string certDir = Directory.Exists("/apex/com.android.conscrypt/cacerts")
                    ? "/apex/com.android.conscrypt/cacerts"
                    : "/system/etc/security/cacerts";
                psiXray.EnvironmentVariables["SSL_CERT_DIR"] = certDir;
                psiXray.EnvironmentVariables["XRAY_LOCATION_ASSET"] = cacheDir;

                _xrayProcess = new System.Diagnostics.Process { StartInfo = psiXray, EnableRaisingEvents = true };
                _xrayProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("android", "XRAY", e.Data.Trim());
                        global::Android.Util.Log.Info("AuraApp", $"[XRAY] {e.Data.Trim()}");
                    }
                };
                _xrayProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("android", "XRAY", e.Data.Trim());
                        global::Android.Util.Log.Info("AuraApp", $"[XRAY] {e.Data.Trim()}");
                    }
                };
                _xrayProcess.Exited += (s, e) =>
                {
                    int exitCode = _xrayProcess?.ExitCode ?? -1;
                    LogService.Instance.Log("android", "WARN", $"Xray core terminated (ExitCode: {exitCode})");
                    global::Android.Util.Log.Warn("AuraApp", $"Xray core terminated (ExitCode: {exitCode})");
                    if (_vpnInterface != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => VpnEngineService.Instance.Disconnect());
                    }
                };

                _xrayProcess.Start();
                _xrayProcess.BeginOutputReadLine();
                _xrayProcess.BeginErrorReadLine();
                LogService.Instance.Log("android", "START", $"Native Xray core started (PID: {_xrayProcess.Id}) for config: {configPath}");
                global::Android.Util.Log.Info("AuraApp", $"Native Xray core started (PID: {_xrayProcess.Id})");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("android", "ERROR", $"Failed to start native Xray core: {ex.Message}");
                global::Android.Util.Log.Error("AuraApp", $"Failed to start native Xray core: {ex.Message}");
            }
        }
        else
        {
            LogService.Instance.Log("android", "WARN", $"Native Xray library not found at: {xrayPath}");
            global::Android.Util.Log.Warn("AuraApp", $"Native Xray library not found at: {xrayPath}");
        }

        // Wait up to 3 seconds for Xray to bind socksPort before launching tun2socks bridge
        bool xrayReady = false;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                using var client = new TcpClient();
                var ar = client.BeginConnect("127.0.0.1", socksPort, null, null);
                if (ar.AsyncWaitHandle.WaitOne(100))
                {
                    client.EndConnect(ar);
                    xrayReady = true;
                    break;
                }
            }
            catch { }
            Thread.Sleep(50);
        }

        if (xrayReady)
        {
            LogService.Instance.Log("android", "READY", $"Xray is actively listening on 127.0.0.1:{socksPort}");
            global::Android.Util.Log.Info("AuraApp", $"Xray is actively listening on 127.0.0.1:{socksPort}");
        }
        else
        {
            LogService.Instance.Log("android", "WARN", $"Xray port 127.0.0.1:{socksPort} not detected yet, launching tun2socks anyway...");
            global::Android.Util.Log.Warn("AuraApp", $"Xray port 127.0.0.1:{socksPort} not detected yet, launching tun2socks anyway...");
        }

        // 2. Start tun2socks bridge (routes TUN FD -> local SOCKS5 proxy)
        if (File.Exists(tun2socksPath) && tunFd > 0)
        {
            try
            {
                // Ensure FD_CLOEXEC is cleared again right before process fork
                fcntl(tunFd, F_SETFD, 0);

                var psiTun = new ProcessStartInfo
                {
                    FileName = tun2socksPath,
                    Arguments = $"--device fd://{tunFd} --proxy socks5://127.0.0.1:{socksPort} --mtu 1400 --tcp-auto-tuning --loglevel info",
                    WorkingDirectory = cacheDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _tun2socksProcess = new System.Diagnostics.Process { StartInfo = psiTun, EnableRaisingEvents = true };
                _tun2socksProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("android", "TUN2SOCKS", e.Data.Trim());
                        global::Android.Util.Log.Info("AuraApp", $"[TUN2SOCKS] {e.Data.Trim()}");
                    }
                };
                _tun2socksProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        LogService.Instance.Log("android", "TUN2SOCKS", e.Data.Trim());
                        global::Android.Util.Log.Info("AuraApp", $"[TUN2SOCKS] {e.Data.Trim()}");
                    }
                };
                _tun2socksProcess.Exited += (s, e) =>
                {
                    int exitCode = _tun2socksProcess?.ExitCode ?? -1;
                    LogService.Instance.Log("android", "WARN", $"tun2socks terminated (ExitCode: {exitCode})");
                    global::Android.Util.Log.Warn("AuraApp", $"tun2socks terminated (ExitCode: {exitCode})");
                    if (_vpnInterface != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => VpnEngineService.Instance.Disconnect());
                    }
                };

                _tun2socksProcess.Start();
                _tun2socksProcess.BeginOutputReadLine();
                _tun2socksProcess.BeginErrorReadLine();
                LogService.Instance.Log("android", "START", $"Native tun2socks bridge started (PID: {_tun2socksProcess.Id}) linking TUN FD:{tunFd} -> SOCKS5:{socksPort}");
                global::Android.Util.Log.Info("AuraApp", $"Native tun2socks bridge started (PID: {_tun2socksProcess.Id}) linking TUN FD:{tunFd} -> SOCKS5:{socksPort}");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("android", "ERROR", $"Failed to start native tun2socks: {ex.Message}");
                global::Android.Util.Log.Error("AuraApp", $"Failed to start native tun2socks: {ex.Message}");
            }
        }
        else
        {
            LogService.Instance.Log("android", "WARN", $"Native tun2socks library not found at: {tun2socksPath}");
            global::Android.Util.Log.Warn("AuraApp", $"Native tun2socks library not found at: {tun2socksPath}");
        }
    }

    private void EnsureAssetsExtracted(string targetDir)
    {
        try
        {
            string[] assetFiles = new[] { "geoip.dat", "geosite.dat" };
            var assets = ApplicationContext?.Assets;
            if (assets == null) return;

            foreach (var file in assetFiles)
            {
                string dest = Path.Combine(targetDir, file);
                if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                {
                    try
                    {
                        using var inStream = assets.Open(file);
                        using var outStream = File.Create(dest);
                        inStream.CopyTo(outStream);
                        LogService.Instance.Log("android", "ASSET", $"Extracted asset {file} to {dest}");
                        global::Android.Util.Log.Info("AuraApp", $"Extracted asset {file} to {dest}");
                    }
                    catch (Exception ex)
                    {
                        global::Android.Util.Log.Warn("AuraApp", $"Could not extract asset {file}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("AuraApp", $"EnsureAssetsExtracted error: {ex.Message}");
        }
    }

    private void StopNativeEngines()
    {
        try
        {
            if (_tun2socksProcess != null && !_tun2socksProcess.HasExited)
            {
                _tun2socksProcess.Kill();
                _tun2socksProcess.Dispose();
                _tun2socksProcess = null;
            }
        }
        catch { }

        try
        {
            if (_xrayProcess != null && !_xrayProcess.HasExited)
            {
                _xrayProcess.Kill();
                _xrayProcess.Dispose();
                _xrayProcess = null;
            }
        }
        catch { }

        KillStaleEngines();
    }

    public void StopTunnel()
    {
        try
        {
            StopNativeEngines();

            _tunnelCts?.Cancel();
            _tunnelCts?.Dispose();
            _tunnelCts = null;

            if (_vpnInterface != null)
            {
                _vpnInterface.Close();
                _vpnInterface.Dispose();
                _vpnInterface = null;
            }

            if (_isForegroundActive)
            {
                StopForeground(StopForegroundFlags.Remove);
                _isForegroundActive = false;
            }

            StopSelf();
            LogService.Instance.Log("android", "STOP", "Android VPN tunnel stopped cleanly.");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "WARN", $"Error stopping VPN: {ex.Message}");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopTunnel();
        _instance = null;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager != null)
            {
                var channel = new NotificationChannel(
                    ChannelId,
                    GetString(Resource.String.vpn_channel_name) ?? "lumen aura VPN Service",
                    NotificationImportance.Low)
                {
                    Description = GetString(Resource.String.vpn_channel_desc) ?? "Active VPN connection notifications"
                };
                manager.CreateNotificationChannel(channel);
            }
        }
    }

    private Notification CreateNotification(string serverName, string protocol)
    {
        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            flags |= PendingIntentFlags.Immutable;
        }

        PendingIntent? pendingDisconnect = null;
        try
        {
            var disconnectIntent = new Intent(this, typeof(AuraVpnService));
            disconnectIntent.SetAction(ActionDisconnect);
            pendingDisconnect = PendingIntent.GetService(this, 1, disconnectIntent, flags);
        }
        catch { }

        PendingIntent? pendingLaunch = null;
        try
        {
            var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "") 
                            ?? new Intent(this, typeof(MainActivity));
            pendingLaunch = PendingIntent.GetActivity(this, 0, launchIntent, flags);
        }
        catch { }

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("lumen aura • Connected")
            .SetContentText($"{serverName} ({protocol})")
            .SetSmallIcon(Resource.Drawable.ic_stat_vpn)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow);

        if (pendingLaunch != null)
        {
            builder.SetContentIntent(pendingLaunch);
        }

        if (pendingDisconnect != null)
        {
            builder.AddAction(Resource.Drawable.ic_stat_vpn, GetString(Resource.String.vpn_disconnect) ?? "Disconnect", pendingDisconnect);
        }

        return builder.Build();
    }
}
