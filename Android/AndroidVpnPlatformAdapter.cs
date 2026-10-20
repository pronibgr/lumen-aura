using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using aura.Models;
using aura.Services;

namespace aura.Android;

public class AndroidVpnPlatformAdapter : IVpnPlatformAdapter
{
    private readonly Activity _activity;

    public AndroidVpnPlatformAdapter(Activity activity)
    {
        _activity = activity;
    }

    public int GetActiveNetworkMark()
    {
        try
        {
            var cm = (ConnectivityManager?)_activity.GetSystemService(Context.ConnectivityService);
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
                            if (int.TryParse(net.ToString(), out int id) && id > 0)
                            {
                                global::Android.Util.Log.Info("AuraApp", $"[NET] Found physical network netId={id} from {net}");
                                return id;
                            }
                            int handleId = (int)(net.NetworkHandle >> 32);
                            if (handleId > 0)
                            {
                                global::Android.Util.Log.Info("AuraApp", $"[NET] Found physical network netId={handleId} from handle");
                                return handleId;
                            }
                        }
                    }
                }
                if (cm.ActiveNetwork != null)
                {
                    if (int.TryParse(cm.ActiveNetwork.ToString(), out int actId) && actId > 0)
                    {
                        global::Android.Util.Log.Info("AuraApp", $"[NET] Found physical network netId={actId} from ActiveNetwork");
                        return actId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("AuraApp", $"[NET] Error finding netId: {ex.Message}");
        }
        return 100;
    }

    public async Task<bool> StartVpnAsync(ServerNode node, int httpPort, int socksPort, string configPath)
    {
        try
        {
            // 1. Check if Android VPN permission has been granted by user
            var prepareIntent = VpnService.Prepare(_activity);
            if (prepareIntent != null)
            {
                MainActivity.VpnPermissionTcs = new TaskCompletionSource<bool>();
                _activity.StartActivityForResult(prepareIntent, 0x1A2B);
                LogService.Instance.Log("android", "AUTH", "Prompting user for Android VPN system permission...");

                // Await user's choice in system dialog
                bool granted = await MainActivity.VpnPermissionTcs.Task;
                if (!granted)
                {
                    LogService.Instance.Log("android", "WARN", "VPN permission was not granted by user.");
                    return false;
                }
            }

            // 2. Start AuraVpnService
            var intent = new Intent(_activity, typeof(AuraVpnService));
            intent.SetAction(AuraVpnService.ActionConnect);
            intent.PutExtra(AuraVpnService.ExtraServerName, node.Name);
            intent.PutExtra(AuraVpnService.ExtraProtocol, node.Protocol);
            intent.PutExtra(AuraVpnService.ExtraHost, node.Host);
            intent.PutExtra(AuraVpnService.ExtraPort, node.Port);
            intent.PutExtra(AuraVpnService.ExtraSocksPort, socksPort);
            intent.PutExtra(AuraVpnService.ExtraHttpPort, httpPort);
            intent.PutExtra(AuraVpnService.ExtraConfigPath, configPath);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                _activity.StartForegroundService(intent);
            }
            else
            {
                _activity.StartService(intent);
            }

            // Short delay to allow TUN setup
            for (int i = 0; i < 20; i++)
            {
                if (AuraVpnService.Instance?.IsTunnelActive == true)
                {
                    return true;
                }
                await Task.Delay(100);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "ERROR", $"Failed to start Android VPN service: {ex.Message}");
            return false;
        }
    }

    public void StopVpn()
    {
        try
        {
            if (AuraVpnService.Instance != null)
            {
                AuraVpnService.Instance.StopTunnel();
            }
            else
            {
                var intent = new Intent(_activity, typeof(AuraVpnService));
                intent.SetAction(AuraVpnService.ActionDisconnect);
                _activity.StartService(intent);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "WARN", $"Failed to send stop intent: {ex.Message}");
        }
    }

    public (long recvBytes, long sentBytes) GetNetworkStats()
    {
        long rx = 0;
        long tx = 0;

        // 1. Try tun0 interface traffic directly (Android 12+ / API 31+)
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                long tunRx = TrafficStats.GetRxBytes("tun0");
                long tunTx = TrafficStats.GetTxBytes("tun0");
                if (tunRx > 0 || tunTx > 0)
                {
                    return (Math.Max(0, tunRx), Math.Max(0, tunTx));
                }
            }
        }
        catch { }

        // 2. Try App UID traffic (All tun2socks, Xray and proxy traffic runs under this UID)
        try
        {
            int myUid = global::Android.OS.Process.MyUid();
            long uidRx = TrafficStats.GetUidRxBytes(myUid);
            long uidTx = TrafficStats.GetUidTxBytes(myUid);
            if (uidRx > 0 || uidTx > 0)
            {
                rx = Math.Max(0, uidRx);
                tx = Math.Max(0, uidTx);
                return (rx, tx);
            }
        }
        catch { }

        // 3. Try TotalRxBytes / TotalTxBytes
        try
        {
            long totalRx = TrafficStats.TotalRxBytes;
            long totalTx = TrafficStats.TotalTxBytes;
            if (totalRx > 0 || totalTx > 0)
            {
                return (Math.Max(0, totalRx), Math.Max(0, totalTx));
            }
        }
        catch { }

        // 4. Fallback to /proc/net/dev if accessible
        try
        {
            if (System.IO.File.Exists("/proc/net/dev"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/net/dev");
                long devRx = 0, devTx = 0;
                foreach (var line in lines)
                {
                    if (line.Contains(':'))
                    {
                        var parts = line.Split(':');
                        string iface = parts[0].Trim();
                        var tokens = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 9 && long.TryParse(tokens[0], out long r) && long.TryParse(tokens[8], out long t))
                        {
                            if (iface.StartsWith("tun"))
                            {
                                return (r, t);
                            }
                            if (!iface.StartsWith("lo"))
                            {
                                devRx += r;
                                devTx += t;
                            }
                        }
                    }
                }
                if (devRx > 0 || devTx > 0)
                {
                    return (devRx, devTx);
                }
            }
        }
        catch { }

        return (rx, tx);
    }
}
