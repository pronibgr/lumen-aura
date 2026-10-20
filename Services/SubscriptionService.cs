using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using aura.Models;

namespace aura.Services;

public class SubscriptionService : ObservableObject
{
    private static SubscriptionService? _instance;
    public static SubscriptionService Instance => _instance ??= new SubscriptionService();

    private readonly HttpClient _httpClient;

    public ObservableCollection<Subscription> Subscriptions { get; } = new();
    public ObservableCollection<Subscription> JsonConfigurations { get; } = new();

    public bool HasSubscriptions => Subscriptions.Count > 0;
    public bool HasJsonConfigs => JsonConfigurations.Count > 0;
    public bool HasAnyConfigs => HasSubscriptions || HasJsonConfigs;

    private readonly string _storagePath;

    public SubscriptionService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        string configDir = System.IO.Path.Combine(appData, "lumen-aura");
        if (!System.IO.Directory.Exists(configDir))
        {
            System.IO.Directory.CreateDirectory(configDir);
        }
        _storagePath = System.IO.Path.Combine(configDir, "subscriptions.json");

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "aura/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");

        LoadSubscriptions();
    }

    public class SubscriptionStore
    {
        public List<Subscription> Subscriptions { get; set; } = new();
        public List<Subscription> JsonConfigurations { get; set; } = new();
    }

    public void LoadSubscriptions()
    {
        try
        {
            if (System.IO.File.Exists(_storagePath))
            {
                string json = System.IO.File.ReadAllText(_storagePath);
                var store = JsonSerializer.Deserialize<SubscriptionStore>(json);
                if (store != null)
                {
                    Subscriptions.Clear();
                    foreach (var sub in store.Subscriptions)
                    {
                        sub.NotifyServersChanged();
                        Subscriptions.Add(sub);
                    }

                    JsonConfigurations.Clear();
                    foreach (var cfg in store.JsonConfigurations)
                    {
                        cfg.NotifyServersChanged();
                        JsonConfigurations.Add(cfg);
                    }

                    ReorderSubscriptions(save: false);
                    OnPropertyChanged(nameof(HasSubscriptions));
                    OnPropertyChanged(nameof(HasJsonConfigs));
                    OnPropertyChanged(nameof(HasAnyConfigs));
                    LogService.Instance.Log("subscriptions", "LOAD", $"Loaded {Subscriptions.Count} subscriptions and {JsonConfigurations.Count} configs from storage.");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("subscriptions", "WARN", $"Could not load stored subscriptions: {ex.Message}");
        }
    }

    public void SaveSubscriptions()
    {
        try
        {
            var store = new SubscriptionStore
            {
                Subscriptions = Subscriptions.ToList(),
                JsonConfigurations = JsonConfigurations.ToList()
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(store, options);
            System.IO.File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("subscriptions", "WARN", $"Could not save subscriptions: {ex.Message}");
        }
    }

    public void PinSubscription(Subscription sub)
    {
        sub.IsPinned = !sub.IsPinned;
        ReorderSubscriptions();
    }

    public void ReorderSubscriptions(bool save = true)
    {
        foreach (var s in Subscriptions)
        {
            s.Name = SubscriptionParser.DecodeSubscriptionTitle(s.Name);
        }
        var sorted = Subscriptions.OrderByDescending(s => s.IsPinned).ThenBy(s => s.Name).ToList();
        Subscriptions.Clear();
        foreach (var s in sorted)
        {
            Subscriptions.Add(s);
        }
        if (save) SaveSubscriptions();
    }

    public async Task PingAllServersInSubscriptionAsync(Subscription sub, string pingType)
    {
        sub.IsPingingAll = true;
        LogService.Instance.Log("subscriptions", "PING", $"Started ping sweep for {sub.Servers.Count} servers in '{sub.Name}'");
        try
        {
            var tasks = sub.Servers.Select(async s =>
            {
                s.IsPinging = true;
                s.NotifyPingUpdated();
                var res = await NetworkService.Instance.PingHostAsync(s.Host, s.Port, pingType);
                s.HasPinged = true;
                s.PingSuccess = res.Success;
                s.PingMs = res.LatencyMs;
                s.IsPinging = false;
                s.NotifyPingUpdated();
            });

            await Task.WhenAll(tasks);
        }
        finally
        {
            sub.IsPingingAll = false;
        }
    }

    private static string? _cachedHwid;
    public static string GetMachineHwid()
    {
        if (!string.IsNullOrEmpty(_cachedHwid)) return _cachedHwid;

        try
        {
            if (System.IO.File.Exists("/etc/machine-id"))
            {
                string id = System.IO.File.ReadAllText("/etc/machine-id").Trim();
                if (!string.IsNullOrEmpty(id)) return _cachedHwid = id;
            }
            if (System.IO.File.Exists("/var/lib/dbus/machine-id"))
            {
                string id = System.IO.File.ReadAllText("/var/lib/dbus/machine-id").Trim();
                if (!string.IsNullOrEmpty(id)) return _cachedHwid = id;
            }
        }
        catch { }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrEmpty(guid)) return _cachedHwid = guid.Replace("-", "").ToLowerInvariant();
            }
        }
        catch { }

        return _cachedHwid = Guid.NewGuid().ToString("N");
    }

    private HttpRequestMessage CreateSubscriptionRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var settings = SettingsService.Instance.GetSettings();

        string userAgent = !string.IsNullOrWhiteSpace(settings.SubscriptionUserAgent) && settings.SubscriptionUserAgent != "lumen-aura/1.0"
            ? settings.SubscriptionUserAgent
            : (!string.IsNullOrWhiteSpace(settings.ConnectionUserAgent) && settings.ConnectionUserAgent != "aura/1.0"
                ? settings.ConnectionUserAgent
                : "Happ/4.2.1/Linux/2609041407559 lumen-aura/1.0");

        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

        string hwid = GetMachineHwid();
        if (!string.IsNullOrEmpty(hwid))
        {
            request.Headers.TryAddWithoutValidation("X-Hwid", hwid);
            LogService.Instance.Log("subscriptions", "HWID", $"Passed X-Hwid header: {hwid}");
        }

        return request;
    }

    public async Task UpdateSubscriptionAsync(Subscription sub)
    {
        sub.IsUpdating = true;
        LogService.Instance.Log("subscriptions", "UPDATE", $"Updating subscription feed: {sub.Url}");
        try
        {
            if (Uri.TryCreate(sub.Url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var request = CreateSubscriptionRequest(sub.Url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    ParseResponseHeaders(response, sub);

                    var content = await response.Content.ReadAsStringAsync();
                    var (nodes, descLines) = SubscriptionParser.ParseContent(content, sub.Id);

                    if (nodes.Count > 0)
                    {
                        sub.Servers.Clear();
                        foreach (var n in nodes)
                        {
                            sub.Servers.Add(n);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(sub.DescriptionText) && descLines.Count > 0)
                    {
                        sub.DescriptionText = string.Join("\n", descLines);
                    }
                }
            }

            sub.LastUpdated = DateTime.Now;
            sub.NotifyServersChanged();
            SaveSubscriptions();
            LogService.Instance.Log("subscriptions", "SUCCESS", $"Successfully updated '{sub.Name}' ({sub.Servers.Count} nodes)");
        }
        catch (Exception ex)
        {
            sub.LastUpdated = DateTime.Now;
            LogService.Instance.Log("subscriptions", "ERROR", $"Failed to update '{sub.Name}': {ex.Message}");
        }
        finally
        {
            sub.IsUpdating = false;
        }
    }

    public async Task<Subscription> AddSubscriptionAsync(string name, string url)
    {
        string initialName = string.IsNullOrWhiteSpace(name) ? "New Subscription" : name.Trim();
        initialName = SubscriptionParser.DecodeSubscriptionTitle(initialName);

        var sub = new Subscription
        {
            Name = initialName,
            Url = url.Trim(),
            IsPinned = false,
            TrafficInfo = "0.0 GB / ∞",
            TrafficPercent = 0.0,
            ExpireDateText = "Истекает: Бессрочно",
            AutoUpdateInfo = $"{DateTime.Now:dd.MM.yyyy HH:mm} | Автообновление - 1ч.",
            LastUpdated = DateTime.Now
        };

        bool fetchedOnline = false;
        if (Uri.TryCreate(sub.Url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            LogService.Instance.Log("subscriptions", "FETCH", $"Fetching subscription from: {sub.Url}");
            try
            {
                using var request = CreateSubscriptionRequest(sub.Url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    ParseResponseHeaders(response, sub);

                    var content = await response.Content.ReadAsStringAsync();
                    var (nodes, descLines) = SubscriptionParser.ParseContent(content, sub.Id);
                    foreach (var n in nodes)
                    {
                        sub.Servers.Add(n);
                    }

                    if (string.IsNullOrWhiteSpace(sub.DescriptionText) && descLines.Count > 0)
                    {
                        sub.DescriptionText = string.Join("\n", descLines);
                    }

                    fetchedOnline = true;
                    LogService.Instance.Log("subscriptions", "PARSE", $"Parsed {nodes.Count} nodes from HTTP feed");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("subscriptions", "ERROR", $"HTTP fetch error: {ex.Message}");
            }
        }

        // Direct raw input fallback
        if (!fetchedOnline)
        {
            var (nodes, descLines) = SubscriptionParser.ParseContent(sub.Url, sub.Id);
            if (nodes.Count > 0)
            {
                foreach (var n in nodes) sub.Servers.Add(n);
                if (descLines.Count > 0) sub.DescriptionText = string.Join("\n", descLines);
            }
        }

        if (sub.Servers.Count == 0)
        {
            throw new InvalidOperationException(
                LocalizationService.Instance.CurrentLanguage == "ru"
                    ? "Не удалось распознать рабочие сервера по указанному адресу"
                    : "No valid server configurations detected from provided URL");
        }

        sub.NotifyServersChanged();
        Subscriptions.Add(sub);
        ReorderSubscriptions();
        SaveSubscriptions();
        OnPropertyChanged(nameof(HasSubscriptions));
        OnPropertyChanged(nameof(HasAnyConfigs));
        return sub;
    }

    public void AddRawConfigSubscription(string name, string rawConfig)
    {
        string finalName = name?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(finalName))
        {
            finalName = SubscriptionParser.DecodeSubscriptionTitle(finalName);
        }

        // Try extracting remarks from JSON if name is not explicitly provided
        if (string.IsNullOrWhiteSpace(finalName))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawConfig);
                if (doc.RootElement.TryGetProperty("remarks", out var rProp) && !string.IsNullOrWhiteSpace(rProp.GetString()))
                {
                    finalName = rProp.GetString()!.Trim();
                }
            }
            catch
            {
                // Not JSON or no remarks
            }
        }

        if (string.IsNullOrWhiteSpace(finalName))
        {
            finalName = $"JSON Config #{JsonConfigurations.Count + 1}";
        }

        var sub = new Subscription
        {
            Name = finalName,
            Url = "local://json_config",
            IsPinned = false,
            TrafficInfo = "∞",
            ExpireDateText = "JSON Profile",
            AutoUpdateInfo = $"{DateTime.Now:dd.MM.yyyy HH:mm} | JSON Config",
            LastUpdated = DateTime.Now
        };

        var (nodes, descLines) = SubscriptionParser.ParseContent(rawConfig, sub.Id);
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException(
                LocalizationService.Instance.CurrentLanguage == "ru"
                    ? "Не удалось распознать конфигурацию (поддерживаются JSON, AWG, WireGuard, VLESS, VMess, Shadowsocks, Trojan)"
                    : "No valid server configurations detected in provided text");
        }

        if (nodes.Count == 1 && !string.IsNullOrWhiteSpace(finalName))
        {
            var (flag, code, country) = CountryFlagHelper.ParseServerFlag(finalName);
            nodes[0].Name = CountryFlagHelper.CleanServerName(finalName);
            if (!string.IsNullOrEmpty(flag) && flag != "🌐") nodes[0].FlagEmoji = flag;
            if (!string.IsNullOrEmpty(code) && code != "GL") nodes[0].CountryCode = code;
            if (!string.IsNullOrEmpty(country) && country != "Global") nodes[0].Country = country;
        }

        foreach (var n in nodes)
        {
            sub.Servers.Add(n);
        }

        if (descLines.Count > 0)
        {
            sub.DescriptionText = string.Join("\n", descLines);
        }

        sub.NotifyServersChanged();
        JsonConfigurations.Add(sub);
        SaveSubscriptions();
        OnPropertyChanged(nameof(HasJsonConfigs));
        OnPropertyChanged(nameof(HasAnyConfigs));
        LogService.Instance.Log("subscriptions", "IMPORT", $"Imported JSON configuration '{finalName}' with {nodes.Count} server nodes");
    }

    public void RemoveSubscription(Subscription sub)
    {
        Subscriptions.Remove(sub);
        JsonConfigurations.Remove(sub);
        SaveSubscriptions();
        OnPropertyChanged(nameof(HasSubscriptions));
        OnPropertyChanged(nameof(HasJsonConfigs));
        OnPropertyChanged(nameof(HasAnyConfigs));

        // Clear active server if it belonged to this deleted subscription
        var active = VpnEngineService.Instance.ActiveServer;
        if (active != null && (active.SubscriptionId == sub.Id || sub.Servers.Any(s => s.Id == active.Id || s == active)))
        {
            VpnEngineService.Instance.ActiveServer = null;
        }

        LogService.Instance.Log("subscriptions", "REMOVE", $"Deleted configuration '{sub.Name}'");
    }

    private void ParseResponseHeaders(HttpResponseMessage response, Subscription sub)
    {
        // 1. subscription-userinfo
        if (response.Headers.TryGetValues("subscription-userinfo", out var userInfoValues))
        {
            var rawUserInfo = userInfoValues.FirstOrDefault() ?? "";
            var (traffic, percent, expire) = SubscriptionParser.ParseUserInfoHeader(rawUserInfo);
            sub.TrafficInfo = traffic;
            sub.TrafficPercent = percent;
            sub.ExpireDateText = expire;
        }

        // 2. announce header (multi-line description or announcement)
        if (response.Headers.TryGetValues("announce", out var announceValues))
        {
            var rawAnnounce = announceValues.FirstOrDefault() ?? "";
            var decodedAnnounce = SubscriptionParser.DecodeSubscriptionTitle(rawAnnounce);
            if (!string.IsNullOrWhiteSpace(decodedAnnounce))
            {
                sub.DescriptionText = decodedAnnounce;
            }
        }

        // 3. Profile Title (profile-title, subscription-title, x-profile-title, content-disposition)
        string? rawTitle = null;

        if (response.Headers.TryGetValues("profile-title", out var titleValues))
        {
            rawTitle = titleValues.FirstOrDefault();
        }
        else if (response.Headers.TryGetValues("subscription-title", out var subTitleValues))
        {
            rawTitle = subTitleValues.FirstOrDefault();
        }
        else if (response.Headers.TryGetValues("x-profile-title", out var xTitleValues))
        {
            rawTitle = xTitleValues.FirstOrDefault();
        }
        else if (response.Content.Headers.TryGetValues("profile-title", out var contentTitleValues))
        {
            rawTitle = contentTitleValues.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            if (response.Content.Headers.ContentDisposition != null)
            {
                var cd = response.Content.Headers.ContentDisposition;
                rawTitle = cd.FileNameStar ?? cd.FileName;
            }
        }

        if (!string.IsNullOrWhiteSpace(rawTitle))
        {
            string decodedTitle = SubscriptionParser.DecodeSubscriptionTitle(rawTitle);
            if (!string.IsNullOrWhiteSpace(decodedTitle))
            {
                sub.Name = decodedTitle;
            }
        }
        else
        {
            sub.Name = SubscriptionParser.DecodeSubscriptionTitle(sub.Name);
        }

        // 4. profile-update-interval
        if (response.Headers.TryGetValues("profile-update-interval", out var intervalValues))
        {
            var interval = intervalValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(interval))
            {
                bool isRu = LocalizationService.Instance.CurrentLanguage == "ru";
                sub.AutoUpdateInfo = isRu
                    ? $"{DateTime.Now:dd.MM.yyyy HH:mm} | Автообновление — {interval}ч."
                    : $"{DateTime.Now:dd.MM.yyyy HH:mm} | Auto-update: {interval}h";
            }
        }

        // 5. support-url & web page
        if (response.Headers.TryGetValues("support-url", out var supValues))
        {
            sub.TelegramUrl = supValues.FirstOrDefault() ?? "";
        }
        if (response.Headers.TryGetValues("profile-web-page-url", out var webValues))
        {
            sub.WebsiteUrl = webValues.FirstOrDefault() ?? "";
        }
    }
}
