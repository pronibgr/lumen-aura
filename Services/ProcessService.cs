using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using aura.Models;

namespace aura.Services;

public interface IAppListProvider
{
    List<ProcessItem> GetApplications(bool includeSystemApps);
}

public class ProcessService
{
    private static ProcessService? _instance;
    public static ProcessService Instance => _instance ??= new ProcessService();

    public IAppListProvider? CustomProvider { get; set; }

    public List<ProcessItem> GetRunningApplications(bool includeSystemApps = false)
    {
        if (CustomProvider != null)
        {
            return CustomProvider.GetApplications(includeSystemApps);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsRunningApplications(includeSystemApps);
        }
        else
        {
            return GetLinuxRunningApplications(includeSystemApps);
        }
    }

    private List<ProcessItem> GetLinuxRunningApplications(bool includeSystemApps)
    {
        var result = new List<ProcessItem>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string name = proc.ProcessName;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    bool isSystem = name.StartsWith("kworker") || name.StartsWith("systemd") ||
                                    name.StartsWith("idle") || name == "kthreadd";

                    // Filter low-level system workers if not requested
                    if (!includeSystemApps && isSystem)
                        continue;

                    if (!seenNames.Add(name)) continue;

                    string displayName = FormatDisplayName(name);
                    result.Add(new ProcessItem
                    {
                        Name = name,
                        DisplayName = displayName,
                        Path = proc.MainModule?.FileName ?? name,
                        Category = isSystem ? "System" : CategorizeProcess(name),
                        IsSystemApp = isSystem
                    });
                }
                catch
                {
                    // Access denied for system process, skip
                }
            }
        }
        catch
        {
            // Fallback list of popular apps if permission restricted
        }

        // Add standard popular apps if not detected
        var commonApps = new (string Name, string Display, string Cat)[]
        {
            ("telegram", "Telegram Desktop", "Messenger"),
            ("chrome", "Google Chrome", "Web Browser"),
            ("firefox", "Mozilla Firefox", "Web Browser"),
            ("discord", "Discord", "Gaming & Voice"),
            ("steam", "Steam Client", "Gaming"),
            ("spotify", "Spotify", "Media & Music"),
            ("code", "Visual Studio Code", "Development"),
            ("obsidian", "Obsidian Notes", "Productivity"),
            ("thunderbird", "Mozilla Thunderbird", "Email"),
            ("vlc", "VLC Media Player", "Media")
        };

        foreach (var (name, display, cat) in commonApps)
        {
            if (!seenNames.Contains(name))
            {
                result.Add(new ProcessItem
                {
                    Name = name,
                    DisplayName = display,
                    Path = name,
                    Category = cat,
                    IsSystemApp = false
                });
            }
        }

        return result.OrderBy(p => p.DisplayName).ToList();
    }

    private List<ProcessItem> GetWindowsRunningApplications(bool includeSystemApps)
    {
        var result = new List<ProcessItem>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var systemBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "smss", "csrss", "wininit", "winlogon", "lsass", "services", "system", "idle",
            "registry", "fontdrvhost", "dwm", "taskhostw", "sihost", "ctfmon", "spoolsv", "conhost",
            "searchindexer", "searchhost", "shellexperiencehost", "runtimebroker", "securityhealthservice",
            "smartscreen", "wmiapsrv", "wlanext", "audiodg", "dashost", "dllhost", "mstsc", "compattelrunner",
            "sedsvc", "backgroundtaskhost", "applicationframehost"
        };

        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string name = proc.ProcessName;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    bool isSystem = systemBlacklist.Contains(name);

                    // Filter session 0 (non-interactive services)
                    try
                    {
                        if (!includeSystemApps && (proc.SessionId == 0 || isSystem)) continue;
                    }
                    catch { }

                    if (systemBlacklist.Contains(name)) continue;
                    if (!seenNames.Add(name)) continue;

                    string displayName = "";
                    string path = name;

                    try
                    {
                        var module = proc.MainModule;
                        if (module != null)
                        {
                            path = module.FileName;
                            string desc = module.FileVersionInfo.FileDescription ?? "";
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                displayName = desc;
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                    {
                        displayName = proc.MainWindowTitle;
                        if (displayName.Length > 40) displayName = displayName.Substring(0, 37) + "...";
                    }

                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = FormatDisplayName(name);
                    }

                    result.Add(new ProcessItem
                    {
                        Name = name,
                        DisplayName = displayName,
                        Path = path,
                        Category = CategorizeProcess(name)
                    });
                }
                catch { }
            }
        }
        catch { }

        // Standard Windows applications
        var popularWindowsApps = new (string Name, string Display, string Cat)[]
        {
            ("telegram", "Telegram Desktop", "Messenger"),
            ("chrome", "Google Chrome", "Web Browser"),
            ("firefox", "Mozilla Firefox", "Web Browser"),
            ("msedge", "Microsoft Edge", "Web Browser"),
            ("discord", "Discord", "Gaming & Voice"),
            ("steam", "Steam Client", "Gaming"),
            ("spotify", "Spotify", "Media & Music"),
            ("code", "Visual Studio Code", "Development"),
            ("obsidian", "Obsidian Notes", "Productivity"),
            ("thunderbird", "Mozilla Thunderbird", "Email"),
            ("vlc", "VLC Media Player", "Media")
        };

        foreach (var (name, display, cat) in popularWindowsApps)
        {
            if (!seenNames.Contains(name))
            {
                result.Add(new ProcessItem
                {
                    Name = name,
                    DisplayName = display,
                    Path = $"{name}.exe",
                    Category = cat
                });
            }
        }

        return result.OrderBy(p => p.DisplayName).ToList();
    }

    private string FormatDisplayName(string processName)
    {
        return processName.ToLowerInvariant() switch
        {
            "telegram" or "telegram-desktop" => "Telegram Desktop",
            "chrome" or "google-chrome" => "Google Chrome",
            "firefox" => "Mozilla Firefox",
            "discord" => "Discord",
            "steam" => "Steam",
            "spotify" => "Spotify",
            "code" => "Visual Studio Code",
            "obsidian" => "Obsidian",
            "slack" => "Slack",
            "msedge" or "microsoft-edge" => "Microsoft Edge",
            _ => char.ToUpperInvariant(processName[0]) + processName.Substring(1)
        };
    }

    private string CategorizeProcess(string processName)
    {
        var lower = processName.ToLowerInvariant();
        if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("edge") || lower.Contains("browser"))
            return "Web Browser";
        if (lower.Contains("telegram") || lower.Contains("discord") || lower.Contains("slack"))
            return "Messenger";
        if (lower.Contains("steam") || lower.Contains("game"))
            return "Gaming";
        if (lower.Contains("code") || lower.Contains("git"))
            return "Development";
        return "Application";
    }
}
