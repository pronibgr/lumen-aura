using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace aura.Services;

public class LocalizationService : ObservableObject
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private string _currentLanguage = "en"; // Default English

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(string.Empty);
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? LanguageChanged;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        bool isRu = _currentLanguage.Equals("ru", StringComparison.OrdinalIgnoreCase);

        if (Translations.TryGetValue(key, out var pair))
        {
            return isRu ? pair.Ru : pair.En;
        }

        return key;
    }

    private static readonly Dictionary<string, (string En, string Ru)> Translations = new()
    {
        // Branding & Navigation
        { "AppTitle", ("lumen aura", "lumen aura") },
        { "AppShortTitle", ("aura", "aura") },
        { "NavDashboard", ("Dashboard", "Главная") },
        { "NavServers", ("Servers", "Серверы") },
        { "NavSubscriptions", ("Subscriptions", "Подписки") },
        { "NavRouting", ("Routing", "Маршрутизация") },
        { "NavSettings", ("Settings", "Параметры") },
        { "NavProtocols", ("Protocols (30+)", "Протоколы (30+)") },
        { "NavLogs", ("Logs", "Логи") },
        { "NavAbout", ("About", "О проекте") },

        // Dashboard
        { "StatusDisconnected", ("disconnected", "отключено") },
        { "StatusConnecting", ("connecting...", "подключение...") },
        { "StatusConnected", ("connected", "подключено") },
        { "ConnectButton", ("CONNECT", "ПОДКЛЮЧИТЬ") },
        { "DisconnectButton", ("DISCONNECT", "ОТКЛЮЧИТЬ") },
        { "ConnectingButton", ("CONNECTING...", "ПОДКЛЮЧЕНИЕ...") },
        { "Duration", ("Connection Time", "Время подключения") },
        { "DownloadSpeed", ("Download", "Входящая скорость") },
        { "UploadSpeed", ("Upload", "Исходящая скорость") },
        { "TotalTraffic", ("Data", "Трафик") },
        { "CurrentServer", ("Active Server", "Текущий сервер") },
        { "SelectedNode", ("Selected Node", "Выбранный узел") },
        { "NoServerSelected", ("No node selected", "Узел не выбран") },
        { "SelectServerPrompt", ("Select a server from the list to connect", "Выберите сервер из списка для подключения") },
        { "Protocol", ("Protocol", "Протокол") },
        { "Ping", ("Ping", "Пинг") },

        // Subscriptions
        { "SubscriptionsTitle", ("Your subscriptions", "Ваши подписки") },
        { "AddSubscription", ("Add New", "Добавить") },
        { "SearchPlaceholder", ("Type text for search", "Поиск по названию или стране...") },
        { "PingAllServers", ("Ping all servers", "Пинг всех серверов") },
        { "UpdateSubscription", ("Update", "Обновить") },
        { "PinSubscription", ("Pin to top", "Закрепить") },
        { "UnpinSubscription", ("Unpin", "Открепить") },
        { "CopySubscriptionUrl", ("Copy link", "Копировать ссылку") },
        { "UrlCopiedNotification", ("Subscription link copied to clipboard", "Ссылка на подписку скопирована в буфер обмена") },
        { "EditSubscription", ("Edit", "Редактировать") },
        { "DeleteSubscription", ("Delete", "Удалить") },
        { "ServersCount", ("servers", "серверов") },
        { "TrafficUsage", ("Traffic", "Трафик") },
        { "Expires", ("Expires", "Истекает") },
        { "SearchServers", ("Search servers by country or protocol...", "Поиск серверов по стране или протоколу...") },
        { "SubscriptionsHeader", ("Subscriptions", "Подписки") },
        { "JsonConfigurationsHeader", ("JSON Configurations", "Конфигурации JSON") },
        { "NoSubscriptionsYet", ("No subscriptions added yet. Click 'Add New' to import your servers.", "Подписки пока не добавлены. Нажмите «Добавить», чтобы загрузить сервера.") },
        { "ErrorNoServersAvailable", ("No servers available. Please add a subscription or configuration first.", "Нет доступных конфигураций. Сначала добавьте подписку или конфигурацию.") },

        // Modal Dialog: Add New
        { "AddModalTitle", ("Add New Server or Subscription", "Добавление подписки или конфигурации") },
        { "AddModalType", ("Type", "Тип") },
        { "AddModalSubscription", ("Subscription", "Подписка") },
        { "AddModalJson", ("JSON / Raw Config", "JSON / Конфиг") },
        { "AddModalNamePrompt", ("Subscription Name (optional):", "Название подписки (опционально):") },
        { "AddModalUrlPrompt", ("Subscription URL / Link:", "Ссылка на подписку:") },
        { "AddModalDownloadButton", ("Download & Add", "Загрузить и добавить") },
        { "AddModalConfigPrompt", ("JSON / AWG / VLESS / VMESS Config:", "Конфигурация JSON / AWG / VLESS / VMESS:") },
        { "AddModalUploadAwg", ("📁 Upload AWG Config (.conf file)", "📁 Загрузить файл конфига AWG (.conf)") },
        { "AddModalImportButton", ("Import Config", "Импортировать конфиг") },

        // Routing Modes
        { "RoutingTitle", ("Traffic Routing", "Маршрутизация трафика") },
        { "RoutingModeAll", ("Route all traffic through VPN", "Пускать весь трафик через VPN") },
        { "RoutingModeAllSub", ("Recommended for full privacy. Encrypts and tunnels all system traffic.", "Рекомендуется для полной конфиденциальности. Шифрует и направляет весь трафик системы.") },
        { "RoutingModeBypass", ("Do NOT route traffic for selected applications", "НЕ пускать трафик в выбранных приложениях") },
        { "RoutingModeBypassSub", ("Bypass list. Selected apps will use your direct ISP connection.", "Список исключений. Выбранные программы используют прямое подключение.") },
        { "RoutingModeOnly", ("Route traffic ONLY for selected applications", "Пускать трафик ТОЛЬКО из выбранных приложений") },
        { "RoutingModeOnlySub", ("Whitelist only. Only apps in the list will be routed through the VPN.", "Только белый список. Через VPN направляются исключительно выбранные программы.") },
        { "RoutingFriendlyNotice", ("💡 Friendly Tip: If you are not sure about these rules, simply leave \"Route all traffic through VPN\". It provides complete protection and security without extra configuration.", "💡 Подсказка: Если вы не уверены в этих правилах, просто оставьте «Пускать весь трафик через VPN». Это обеспечит полную защиту без дополнительных настроек.") },
        { "SelectedAppsHeader", ("Selected Applications", "Выбранные приложения") },
        { "AddApplication", ("Add Application", "Добавить приложение") },
        { "NoAppsSelected", ("No applications selected yet. Click 'Add Application' to select.", "Приложения пока не добавлены. Нажмите «Добавить приложение».") },
        { "RemoveApp", ("Remove", "Удалить") },

        // Settings Menu & Submenus
        { "SettingsTitle", ("Settings", "Параметры") },
        { "SettingsGeneral", ("General", "Основные") },
        { "SettingsLanguage", ("Language", "Язык") },
        { "SettingsLanguageSub", ("Choose application display language", "Выберите язык интерфейса приложения") },
        { "SettingsUserAgent", ("Connection User-Agent", "User-Agent подключения") },
        { "SettingsUserAgentSub", ("User-Agent string sent during connection establishment and handshakes", "Строка User-Agent, отправляемая при установке соединения") },
        { "SettingsRoutingModes", ("Traffic Routing Modes", "Режимы маршрутизации трафика") },
        { "SettingsFragmentation", ("Enable fragmentation", "Включить фрагментацию") },
        { "SettingsFragmentationSub", ("Splits TLS client hello packets to evade deep packet inspection (DPI)", "Разбивает пакеты TLS Client Hello для обхода DPI") },
        { "SettingsMultiplexer", ("Enable multiplexer (MUX)", "Включить мультиплексор (MUX)") },
        { "SettingsMultiplexerSub", ("Multiplex multiple TCP connections over a single transport stream", "Мультиплексирование нескольких TCP соединений в единый поток") },
        { "SettingsAdvanced", ("Advanced", "Расширенные") },
        { "SettingsSubscriptions", ("Subscriptions", "Подписки") },
        { "SettingsPing", ("Ping", "Пинг") },
        { "SettingsNetwork", ("Network & Ports", "Сеть и порты") },
        { "SettingsAllowLanSub", ("Share local proxy port with other devices on your local Wi-Fi network", "Разрешить устройствам в локальной сети подключаться к прокси") },
        { "SettingsAutoUpdateSub", ("Periodically fetch updated server lists from subscription URLs in the background", "Периодически обновлять списки серверов подписок в фоне") },
        { "SettingsPingTypeSub", ("Select protocol mechanism used for calculating node latency", "Выберите протокол для замера задержки до серверов") },
        { "SettingsPingDisplaySub", ("Number (ms) or visual indicator", "Числовое значение (мс) или статусная иконка") },
        { "SettingsKillSwitchSub", ("Block internet access immediately if VPN connection drops unexpectedly", "Блокировать интернет при неожиданном разрыве VPN-соединения") },

        // Advanced Settings
        { "AdvBreadcrumb", ("Settings > Advanced", "Параметры > Расширенные") },
        { "AdvOtherSettings", ("Other settings", "Другие настройки") },
        { "AdvLocalDns", ("Use local DNS", "Использовать локальный DNS") },
        { "AdvDnsLeak", ("DNS leak protection", "Предотвращение утечек DNS") },
        { "AdvPacketSniffing", ("Packet analysis", "Анализ пакетов") },
        { "AdvAutoStart", ("Auto-start", "Автозапуск") },
        { "AdvSystemProxy", ("System proxy", "Системный прокси") },
        { "AdvBypassLanProxy", ("Do not use system proxy for local addresses", "Не использовать системный прокси для локальных адресов") },
        { "AdvTun", ("TUN", "TUN") },
        { "AdvTunProvider", ("TUN Provider", "Провайдер TUN") },
        { "AdvTunDns", ("Enable DNS for TUN", "Включить DNS для TUN") },
        { "AdvAllowLan", ("Allow connections from LAN", "Разрешить подключения из LAN") },
        { "AdvCurrentIp", ("Current IP:", "Текущий IP:") },

        // Subscriptions Submenu
        { "SubAutoUpdate", ("Auto-update subscriptions", "Автообновление подписок") },
        { "SubIntervalHours", ("Update interval (hours)", "Интервал обновления (часы)") },
        { "SubNotifications", ("Update notifications", "Уведомления об обновлениях") },
        { "SubTimeoutSec", ("Request timeout (sec)", "Таймаут запроса (сек)") },
        { "SubUpdateOnLaunch", ("Update on launch", "Обновлять при открытии программы") },
        { "SubPingOnLaunch", ("Ping on launch", "Пинг при открытии программы") },
        { "SubUserAgent", ("User Agent", "Агент пользователя") },

        // Ping Submenu
        { "PingTypeHeader", ("Ping type", "Тип пинга") },
        { "PingModeViaProxy", ("Mode (via proxy)", "Режим (via proxy)") },
        { "PingDisplayMode", ("Ping display", "Отображение пинга") },
        { "PingDisplayNumber", ("Number (e.g. 45 ms)", "Число") },
        { "PingDisplayIcon", ("Icon (Checkmark / Cross)", "Иконка") },

        // Extra Settings
        { "SettingsHttpPort", ("Inbound HTTP Port", "Входящий HTTP порт") },
        { "SettingsSocksPort", ("Inbound SOCKS5 Port", "Входящий SOCKS5 порт") },
        { "SettingsMixedPort", ("Inbound Mixed Port", "Смешанный порт") },
        { "SettingsCustomDns", ("Custom Primary DNS", "Основной DNS сервер") },
        { "SettingsFallbackDns", ("Fallback DNS", "Резервный DNS") },
        { "SettingsKillSwitch", ("Kill Switch (block traffic on disconnect)", "Kill Switch (блокировка при разрыве)") },

        // Logs
        { "LogsTitle", ("Logs", "Логи") },
        { "LogsReportHint", ("Press Ctrl+R to create report", "Нажмите Ctrl+R, чтобы создать отчёт") },
        { "LogTabMain", ("Main Log", "Основной лог") },
        { "LogTabCore", ("Core Log", "Лог ядра") },
        { "LogTabTunnel", ("Tunnel Log", "Лог туннеля") },
        { "LogTabAntiFilter", ("AntiFilter Log", "Лог AntiFilter") },
        { "LogTabSubscriptions", ("Subscriptions Log", "Лог подписок") },
        { "LogTabService", ("Service Log", "Лог службы") },
        { "LogTabPush", ("Push Log", "Лог пушей") },
        { "LogsCopyAll", ("Copy all logs", "Скопировать всё") },
        { "LogsGenerateReport", ("Generate report", "Создать отчёт") },
        { "LogsClear", ("Clear logs", "Очистить журнал") },

        // About Project (per Point 14)
        { "AboutTitle", ("About lumen aura", "О программе lumen aura") },
        { "AboutCreator", ("Creator & Lead Developer:", "Создатель и ведущий разработчик:") },
        { "AboutGitHub", ("GitHub: @pronibgr", "GitHub: @pronibgr") },
        { "AboutDescription", ("Universal high-performance VPN and proxy client in Material 3 Expressive style.", "Универсальный высокопроизводительный VPN и прокси-клиент в стиле Material 3 Expressive.") },
        { "AboutProtocols", ("Supported: VLESS, VMess, Trojan, Shadowsocks, Hysteria 2, TUIC, AmneziaWG (all versions), WireGuard, OpenVPN, and 20+ more.", "Поддерживается: VLESS, VMess, Trojan, Shadowsocks, Hysteria 2, TUIC, AmneziaWG (любые версии), WireGuard, OpenVPN и еще 20+ технологий.") },

        { "ShowSystemApps", ("Show system apps", "Показывать системные") },
        { "SettingsTheme", ("Theme", "Тема оформления") },
        { "SettingsThemeSub", ("Choose light or dark theme", "Выберите светлую или тёмную тему") },
        { "ThemeLight", ("Light", "Светлая") },
        { "ThemeDark", ("Dark", "Тёмная") },
        { "BackToMain", ("Back to Main", "Назад на главный экран") },
        { "Back", ("Back", "Назад") },

        // Common
        { "Save", ("Save", "Сохранить") },
        { "Cancel", ("Cancel", "Отмена") },
        { "Close", ("Close", "Закрыть") },
        { "Search", ("Search", "Поиск") },
        { "Copy", ("Copy", "Копировать") },
        { "Copied", ("Copied!", "Скопировано!") },
        { "Active", ("Active", "Активно") },
        { "On", ("On", "Вкл.") },
        { "Off", ("Off", "Выкл.") },
        { "Enabled", ("Enabled", "Включено") },
        { "Disabled", ("Disabled", "Отключено") },
        { "SettingsSaved", ("Settings saved", "Настройки сохранены") },
        { "CheckPing", ("Check ping", "Проверить пинг") },
        { "CopyJson", ("Copy JSON", "Копировать JSON") },
        { "DeleteServer", ("Delete", "Удалить") },
        { "ServerDeleted", ("Server deleted", "Сервер удален") },
        { "JsonCopied", ("JSON copied to clipboard", "JSON скопирован в буфер") },
        { "SpeedUnit", ("Speed Unit", "Единицы измерения скорости") },
        { "SpeedUnitSub", ("Display speed in Bytes/s or Bits/s", "Отображение скорости в байтах/с или битах/с") },
        { "SpeedUnitBytes", ("Bytes (KB/s, MB/s)", "Байты (КБ/с, МБ/с)") },
        { "SpeedUnitBits", ("Bits (Kbps, Mbps)", "Биты (Кбит/с, Мбит/с)") },
        { "UploadConfFile", ("Upload .conf file", "Загрузить файл .conf") }
    };
}
