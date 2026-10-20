using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace aura.Services;

public class SystemProxyService
{
    private static SystemProxyService? _instance;
    public static SystemProxyService Instance => _instance ??= new SystemProxyService();

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    public void EnableProxy(int httpPort = 10809, int socksPort = 10808)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                EnableLinuxProxy(httpPort, socksPort);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EnableWindowsProxy(httpPort, socksPort);
            }

            // Also set process-level environment variables
            Environment.SetEnvironmentVariable("http_proxy", $"http://127.0.0.1:{httpPort}");
            Environment.SetEnvironmentVariable("https_proxy", $"http://127.0.0.1:{httpPort}");
            Environment.SetEnvironmentVariable("all_proxy", $"socks5://127.0.0.1:{socksPort}");
            Environment.SetEnvironmentVariable("HTTP_PROXY", $"http://127.0.0.1:{httpPort}");
            Environment.SetEnvironmentVariable("HTTPS_PROXY", $"http://127.0.0.1:{httpPort}");
            Environment.SetEnvironmentVariable("ALL_PROXY", $"socks5://127.0.0.1:{socksPort}");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("tunnel", "PROXY-ERR", $"Failed to configure system proxy: {ex.Message}");
        }
    }

    public void DisableProxy()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                DisableLinuxProxy();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DisableWindowsProxy();
            }

            Environment.SetEnvironmentVariable("http_proxy", null);
            Environment.SetEnvironmentVariable("https_proxy", null);
            Environment.SetEnvironmentVariable("all_proxy", null);
            Environment.SetEnvironmentVariable("HTTP_PROXY", null);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null);
            Environment.SetEnvironmentVariable("ALL_PROXY", null);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("tunnel", "PROXY-ERR", $"Failed to disable system proxy: {ex.Message}");
        }
    }

    private void EnableLinuxProxy(int httpPort, int socksPort)
    {
        // 1. KDE Plasma (kwriteconfig6)
        try
        {
            RunProcess("kwriteconfig6", $"--file kioslaverc --group \"Proxy Settings\" --key ProxyType 1");
            RunProcess("kwriteconfig6", $"--file kioslaverc --group \"Proxy Settings\" --key httpProxy \"http://127.0.0.1:{httpPort}\"");
            RunProcess("kwriteconfig6", $"--file kioslaverc --group \"Proxy Settings\" --key httpsProxy \"http://127.0.0.1:{httpPort}\"");
            RunProcess("kwriteconfig6", $"--file kioslaverc --group \"Proxy Settings\" --key socksProxy \"socks://127.0.0.1:{socksPort}\"");
            RunProcess("dbus-send", "--type=signal /KIO/Scheduler org.kde.KIO.Scheduler.reparseSlaveConfiguration string:\"\"");
        }
        catch { }

        // 2. GNOME / GSettings (used by Chrome, Firefox, Electron apps)
        try
        {
            RunProcess("gsettings", "set org.gnome.system.proxy mode 'manual'");
            RunProcess("gsettings", $"set org.gnome.system.proxy.http host '127.0.0.1'");
            RunProcess("gsettings", $"set org.gnome.system.proxy.http port {httpPort}");
            RunProcess("gsettings", $"set org.gnome.system.proxy.https host '127.0.0.1'");
            RunProcess("gsettings", $"set org.gnome.system.proxy.https port {httpPort}");
            RunProcess("gsettings", $"set org.gnome.system.proxy.socks host '127.0.0.1'");
            RunProcess("gsettings", $"set org.gnome.system.proxy.socks port {socksPort}");
        }
        catch { }

        // 3. Systemd User Session Environment (inherited by KDE Konsole and terminal apps)
        try
        {
            RunProcess("systemctl", $"--user set-environment http_proxy=http://127.0.0.1:{httpPort} https_proxy=http://127.0.0.1:{httpPort} all_proxy=socks5://127.0.0.1:{socksPort} HTTP_PROXY=http://127.0.0.1:{httpPort} HTTPS_PROXY=http://127.0.0.1:{httpPort} ALL_PROXY=socks5://127.0.0.1:{socksPort}");
        }
        catch { }

        // 4. Write standard user environment configuration (environment.d)
        try
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string envDir = Path.Combine(userHome, ".config", "environment.d");
            if (!Directory.Exists(envDir)) Directory.CreateDirectory(envDir);
            string envFile = Path.Combine(envDir, "99-aura-proxy.conf");
            File.WriteAllText(envFile,
                $"http_proxy=http://127.0.0.1:{httpPort}\n" +
                $"https_proxy=http://127.0.0.1:{httpPort}\n" +
                $"all_proxy=socks5://127.0.0.1:{socksPort}\n" +
                $"HTTP_PROXY=http://127.0.0.1:{httpPort}\n" +
                $"HTTPS_PROXY=http://127.0.0.1:{httpPort}\n" +
                $"ALL_PROXY=socks5://127.0.0.1:{socksPort}\n");

            string auraDir = Path.Combine(userHome, ".config", "lumen-aura");
            if (!Directory.Exists(auraDir)) Directory.CreateDirectory(auraDir);
            string shellEnv = Path.Combine(auraDir, "proxy_env.sh");
            File.WriteAllText(shellEnv,
                $"export http_proxy=http://127.0.0.1:{httpPort}\n" +
                $"export https_proxy=http://127.0.0.1:{httpPort}\n" +
                $"export all_proxy=socks5://127.0.0.1:{socksPort}\n" +
                $"export HTTP_PROXY=http://127.0.0.1:{httpPort}\n" +
                $"export HTTPS_PROXY=http://127.0.0.1:{httpPort}\n" +
                $"export ALL_PROXY=socks5://127.0.0.1:{socksPort}\n");
        }
        catch { }
    }

    private void DisableLinuxProxy()
    {
        // 1. KDE Plasma
        try
        {
            RunProcess("kwriteconfig6", "--file kioslaverc --group \"Proxy Settings\" --key ProxyType 0");
            RunProcess("dbus-send", "--type=signal /KIO/Scheduler org.kde.KIO.Scheduler.reparseSlaveConfiguration string:\"\"");
        }
        catch { }

        // 2. GNOME
        try
        {
            RunProcess("gsettings", "set org.gnome.system.proxy mode 'none'");
        }
        catch { }

        // 3. Systemd User Session Environment
        try
        {
            RunProcess("systemctl", "--user unset-environment http_proxy https_proxy all_proxy HTTP_PROXY HTTPS_PROXY ALL_PROXY");
        }
        catch { }

        // 4. Clean up environment.d and shell exports
        try
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string envFile = Path.Combine(userHome, ".config", "environment.d", "99-aura-proxy.conf");
            if (File.Exists(envFile)) File.Delete(envFile);

            string shellEnv = Path.Combine(userHome, ".config", "lumen-aura", "proxy_env.sh");
            if (File.Exists(shellEnv)) File.Delete(shellEnv);
        }
        catch { }
    }

    private void EnableWindowsProxy(int httpPort, int socksPort)
    {
        try
        {
            // Configure Internet Settings in Registry
            RunProcess("reg", $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 1 /f");
            RunProcess("reg", $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyServer /t REG_SZ /d \"127.0.0.1:{httpPort}\" /f");
            RunProcess("reg", $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyOverride /t REG_SZ /d \"<local>\" /f");

            NotifyWinInet();

            // Set User-level environment variables for CLI tools (PowerShell, CMD, Git, curl, pip)
            Environment.SetEnvironmentVariable("http_proxy", $"http://127.0.0.1:{httpPort}", EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("https_proxy", $"http://127.0.0.1:{httpPort}", EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("all_proxy", $"socks5://127.0.0.1:{socksPort}", EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTP_PROXY", $"http://127.0.0.1:{httpPort}", EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", $"http://127.0.0.1:{httpPort}", EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("ALL_PROXY", $"socks5://127.0.0.1:{socksPort}", EnvironmentVariableTarget.User);
        }
        catch { }
    }

    private void DisableWindowsProxy()
    {
        try
        {
            RunProcess("reg", $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f");

            NotifyWinInet();

            Environment.SetEnvironmentVariable("http_proxy", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("https_proxy", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("all_proxy", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTP_PROXY", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("ALL_PROXY", null, EnvironmentVariableTarget.User);
        }
        catch { }
    }

    private static void NotifyWinInet()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
        }
        catch { }
    }

    private static void RunProcess(string filename, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(filename, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(1000);
        }
        catch { }
    }
}
