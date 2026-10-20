using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace aura.Services;

public class NetworkService
{
    private static NetworkService? _instance;
    public static NetworkService Instance => _instance ??= new NetworkService();

    public string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }

        return "192.168.1.100";
    }

    public async Task<(bool Success, int LatencyMs)> PingHostAsync(string host, int port, string pingType, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(host) || host == "0.0.0.0" || port <= 0)
        {
            return (false, -1);
        }

        string cleanHost = host.Trim().Trim('[', ']');

        // 1. If host is TLS/HTTPS/Reality/VLESS/VMess: measure real TLS Client-Hello round-trip latency
        try
        {
            int targetPort = port > 0 ? port : 443;
            using var tcp = new TcpClient();
            var sw = Stopwatch.StartNew();

            var connectTask = tcp.ConnectAsync(cleanHost, targetPort);
            var delayTask = Task.Delay(timeoutMs);
            if (await Task.WhenAny(connectTask, delayTask) == connectTask && tcp.Connected)
            {
                try
                {
                    using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
                    using var cts = new CancellationTokenSource(timeoutMs);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = cleanHost,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, cts.Token);

                    sw.Stop();
                    int tlsLatency = (int)sw.ElapsedMilliseconds;
                    if (tlsLatency > 0)
                    {
                        return (true, tlsLatency);
                    }
                }
                catch
                {
                    // Non-TLS plain TCP service
                    sw.Stop();
                    int tcpLatency = (int)sw.ElapsedMilliseconds;
                    // Filter out local transparent proxy intercepts (< 5ms)
                    if (tcpLatency >= 5)
                    {
                        return (true, tcpLatency);
                    }
                }
            }
        }
        catch
        {
        }

        // 2. HTTP Probe via active VPN proxy (if engine is connected)
        if (VpnEngineService.Instance.IsConnected)
        {
            try
            {
                int activePort = VpnEngineService.Instance.ActiveHttpPort > 0 ? VpnEngineService.Instance.ActiveHttpPort : 20809;
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://127.0.0.1:{activePort}"),
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
                var sw = Stopwatch.StartNew();
                using var resp = await http.GetAsync("http://www.gstatic.com/generate_204");
                sw.Stop();
                if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 204)
                {
                    int latency = (int)sw.ElapsedMilliseconds;
                    return (true, latency > 0 ? latency : 1);
                }
            }
            catch
            {
            }
        }

        // 3. Fallback ICMP (only if not intercepted locally)
        if (pingType == "ICMP")
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(cleanHost, timeoutMs);
                if (reply.Status == IPStatus.Success && reply.RoundtripTime > 10)
                {
                    return (true, (int)reply.RoundtripTime);
                }
            }
            catch
            {
            }
        }

        // If ping failed or is unreachable, strictly return failure (displays 'n/a')
        return (false, -1);
    }
}
