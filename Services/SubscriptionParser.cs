using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;
using aura.Models;

namespace aura.Services;

public static class SubscriptionParser
{
    public static (List<ServerNode> nodes, List<string> descriptionLines) ParseContent(string rawContent, string subscriptionId)
    {
        var nodes = new List<ServerNode>();
        var descriptionLines = new List<string>();

        if (string.IsNullOrWhiteSpace(rawContent))
            return (nodes, descriptionLines);

        // Try base64 decoding if content looks like a base64 feed
        string text = DecodeIfBase64(rawContent.Trim());

        // 1. Direct support for AmneziaWG / WireGuard .conf (INI) files
        if (text.Contains("[Interface]", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("[Peer]", StringComparison.OrdinalIgnoreCase))
        {
            var awgNode = ParseAwgConf(text, subscriptionId);
            if (awgNode != null)
            {
                nodes.Add(awgNode);
                descriptionLines.Add($"AmneziaWG / WireGuard: {awgNode.Host}:{awgNode.Port}");
                if (awgNode.AwgConfig != null)
                {
                    descriptionLines.Add($"Detected: {awgNode.AwgConfig.DetectedVersion}");
                    descriptionLines.Add($"AllowedIPs: {awgNode.AwgConfig.AllowedIPs}");
                }
                return (nodes, descriptionLines);
            }
        }

        // 2. Direct support for JSON configs (Array of configs or single config)
        string trimmed = text.Trim();
        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) || (trimmed.StartsWith('{') && trimmed.EndsWith('}')))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // Feed of multiple server configurations (JSON array / subscription feed)
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.ValueKind != JsonValueKind.Object) continue;

                        string itemRemarks = elem.TryGetProperty("remarks", out var rProp) ? rProp.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(itemRemarks) && elem.TryGetProperty("name", out var nProp))
                        {
                            itemRemarks = nProp.GetString() ?? "";
                        }

                        if (elem.TryGetProperty("outbounds", out var outbounds) && outbounds.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement? chosenOb = null;
                            foreach (var ob in outbounds.EnumerateArray())
                            {
                                string proto = ob.TryGetProperty("protocol", out var pProp) ? pProp.GetString() ?? "" : "";
                                proto = proto.ToLowerInvariant();
                                if (proto == "freedom" || proto == "blackhole" || proto == "direct" || proto == "block" || proto == "dns")
                                    continue;

                                string tag = ob.TryGetProperty("tag", out var tProp) ? tProp.GetString() ?? "" : "";
                                if (tag == "proxy" || !chosenOb.HasValue)
                                {
                                    chosenOb = ob;
                                    if (tag == "proxy") break;
                                }
                            }

                            if (chosenOb.HasValue)
                            {
                                var ob = chosenOb.Value;
                                string proto = ob.TryGetProperty("protocol", out var pProp) ? pProp.GetString() ?? "VLESS" : "VLESS";
                                string tag = ob.TryGetProperty("tag", out var tProp) ? tProp.GetString() ?? "proxy" : "proxy";

                                string server = "127.0.0.1";
                                int port = 443;

                                if (ob.TryGetProperty("settings", out var stProp))
                                {
                                    if (stProp.TryGetProperty("vnext", out var vnProp) && vnProp.ValueKind == JsonValueKind.Array)
                                    {
                                        var firstVn = vnProp.EnumerateArray().FirstOrDefault();
                                        if (firstVn.ValueKind == JsonValueKind.Object)
                                        {
                                            if (firstVn.TryGetProperty("address", out var addrProp)) server = addrProp.GetString() ?? "";
                                            if (firstVn.TryGetProperty("port", out var ptProp)) port = ptProp.GetInt32();
                                        }
                                    }
                                    else if (stProp.TryGetProperty("servers", out var svProp) && svProp.ValueKind == JsonValueKind.Array)
                                    {
                                        var firstSv = svProp.EnumerateArray().FirstOrDefault();
                                        if (firstSv.ValueKind == JsonValueKind.Object)
                                        {
                                            if (firstSv.TryGetProperty("address", out var addrProp)) server = addrProp.GetString() ?? "";
                                            if (firstSv.TryGetProperty("port", out var ptProp)) port = ptProp.GetInt32();
                                        }
                                    }
                                }

                                string transport = "TCP";
                                string security = "REALITY";
                                if (ob.TryGetProperty("streamSettings", out var ssProp))
                                {
                                    if (ssProp.TryGetProperty("network", out var nwProp)) transport = nwProp.GetString()?.ToUpperInvariant() ?? "TCP";
                                    if (ssProp.TryGetProperty("security", out var scProp)) security = scProp.GetString()?.ToUpperInvariant() ?? "REALITY";
                                }

                                string label = !string.IsNullOrWhiteSpace(itemRemarks) ? itemRemarks : (!string.IsNullOrWhiteSpace(tag) ? tag : $"{proto.ToUpperInvariant()} {server}:{port}");
                                var (flag, code, country) = CountryFlagHelper.ParseServerFlag(label);
                                string cleanName = CountryFlagHelper.CleanServerName(label);

                                nodes.Add(new ServerNode
                                {
                                    SubscriptionId = subscriptionId,
                                    Name = cleanName,
                                    OutboundTag = tag,
                                    Country = country,
                                    CountryCode = code,
                                    FlagEmoji = flag,
                                    Host = server,
                                    Port = port,
                                    Protocol = proto.ToUpperInvariant(),
                                    TransportInfo = transport,
                                    SecurityInfo = security,
                                    TransportSecurity = $"{proto.ToUpperInvariant()} / {transport} / {security}",
                                    RawConfig = elem.GetRawText(),
                                    PingMs = -1,
                                    PingSuccess = false,
                                    HasPinged = false
                                });
                            }
                        }
                    }

                    if (nodes.Count > 0)
                    {
                        descriptionLines.Add($"Subscription Feed: {nodes.Count} servers");
                        return (nodes, descriptionLines);
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    string remarks = doc.RootElement.TryGetProperty("remarks", out var remProp) ? remProp.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(remarks) && doc.RootElement.TryGetProperty("name", out var nmProp))
                    {
                        remarks = nmProp.GetString() ?? "";
                    }

                    if (doc.RootElement.TryGetProperty("outbounds", out var outbounds) && outbounds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ob in outbounds.EnumerateArray())
                        {
                            string proto = ob.TryGetProperty("protocol", out var ptlProp)
                                ? ptlProp.GetString() ?? ""
                                : (ob.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "");

                            proto = proto.ToLowerInvariant();
                            if (proto == "freedom" || proto == "blackhole" || proto == "direct" || proto == "block" || proto == "dns")
                                continue;

                            string tag = ob.TryGetProperty("tag", out var tgProp) ? tgProp.GetString() ?? "" : "";
                            string server = "";
                            int port = 443;

                            if (ob.TryGetProperty("server", out var srvProp))
                            {
                                server = srvProp.GetString() ?? "";
                                if (ob.TryGetProperty("server_port", out var prtProp)) port = prtProp.GetInt32();
                            }
                            else if (ob.TryGetProperty("settings", out var stProp))
                            {
                                if (stProp.TryGetProperty("vnext", out var vnProp) && vnProp.ValueKind == JsonValueKind.Array)
                                {
                                    var firstVn = vnProp.EnumerateArray().FirstOrDefault();
                                    if (firstVn.ValueKind == JsonValueKind.Object)
                                    {
                                        if (firstVn.TryGetProperty("address", out var addrProp)) server = addrProp.GetString() ?? "";
                                        if (firstVn.TryGetProperty("port", out var ptProp)) port = ptProp.GetInt32();
                                    }
                                }
                                else if (stProp.TryGetProperty("servers", out var svProp) && svProp.ValueKind == JsonValueKind.Array)
                                {
                                    var firstSv = svProp.EnumerateArray().FirstOrDefault();
                                    if (firstSv.ValueKind == JsonValueKind.Object)
                                    {
                                        if (firstSv.TryGetProperty("address", out var addrProp)) server = addrProp.GetString() ?? "";
                                        if (firstSv.TryGetProperty("port", out var ptProp)) port = ptProp.GetInt32();
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(server))
                                continue;

                            string transport = "TCP";
                            string security = "TLS";
                            if (ob.TryGetProperty("streamSettings", out var ssProp))
                            {
                                if (ssProp.TryGetProperty("network", out var nwProp)) transport = nwProp.GetString()?.ToUpperInvariant() ?? "TCP";
                                if (ssProp.TryGetProperty("security", out var scProp)) security = scProp.GetString()?.ToUpperInvariant() ?? "TLS";
                            }

                            string labelForFlag = !string.IsNullOrWhiteSpace(remarks) ? remarks : (!string.IsNullOrWhiteSpace(tag) ? tag : server);
                            var (flag, code, country) = CountryFlagHelper.ParseServerFlag(labelForFlag);

                            string nodeTitle = !string.IsNullOrWhiteSpace(tag)
                                ? tag
                                : $"{proto.ToUpperInvariant()} {server}:{port}";

                            nodes.Add(new ServerNode
                            {
                                SubscriptionId = subscriptionId,
                                Name = CountryFlagHelper.CleanServerName(nodeTitle),
                                OutboundTag = tag,
                                Country = country,
                                CountryCode = code,
                                FlagEmoji = flag,
                                Host = server,
                                Port = port,
                                Protocol = proto.ToUpperInvariant(),
                                TransportInfo = transport,
                                SecurityInfo = security,
                                TransportSecurity = $"{proto.ToUpperInvariant()} / {transport} / {security}",
                                RawConfig = trimmed,
                                PingMs = -1,
                                PingSuccess = false,
                                HasPinged = false
                            });
                        }

                        if (nodes.Count > 0)
                        {
                            if (!string.IsNullOrWhiteSpace(remarks)) descriptionLines.Add(remarks);
                            descriptionLines.Add($"JSON Profile: {nodes.Count} server nodes");
                            return (nodes, descriptionLines);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Lines starting with #, //, or ; are remarks/comments
            if (line.StartsWith('#') || line.StartsWith("//") || line.StartsWith(';'))
            {
                var cleanComment = line.TrimStart('#', '/', ';', ' ').Trim();
                if (!string.IsNullOrWhiteSpace(cleanComment))
                    descriptionLines.Add(cleanComment);
                continue;
            }

            // Check for URI nodes
            if (line.Contains("://"))
            {
                var parsedNode = TryParseUriNode(line, subscriptionId);
                if (parsedNode != null)
                {
                    nodes.Add(parsedNode);
                }
            }
        }

        return (nodes, descriptionLines);
    }

    public static string DecodeIfBase64(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        try
        {
            var cleaned = input.Replace(" ", "").Replace("\r", "").Replace("\n", "");
            if (cleaned.Length % 4 != 0)
            {
                cleaned += new string('=', 4 - (cleaned.Length % 4));
            }

            var bytes = Convert.FromBase64String(cleaned);
            var decoded = Encoding.UTF8.GetString(bytes);

            if (decoded.Contains("://") || decoded.Contains("vmess") || decoded.Contains("vless") || decoded.Contains('\n') || decoded.Contains("Подписка"))
            {
                return decoded;
            }
        }
        catch
        {
            // Not base64
        }

        return input;
    }

    /// <summary>
    /// Detects and decodes Base64, RFC-2047 MIME, URL-encoding, or raw Base64 in subscription headers and titles.
    /// </summary>
    public static string DecodeSubscriptionTitle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string text = input.Trim().Trim('"', '\'');

        // 1. Content-Disposition filename / filename* format
        if (text.StartsWith("filename*=", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring("filename*=".Length).Trim('"', '\'');
            int quoteIdx = text.IndexOf("''", StringComparison.Ordinal);
            if (quoteIdx >= 0)
            {
                text = text.Substring(quoteIdx + 2);
            }
            try
            {
                text = Uri.UnescapeDataString(text);
            }
            catch { }
        }
        else if (text.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring("filename=".Length).Trim('"', '\'');
        }

        // 2. RFC 2047 MIME encoded words: =?charset?encoding?encoded_text?= (e.g. =?UTF-8?B?...?=)
        if (text.StartsWith("=?") && text.EndsWith("?="))
        {
            try
            {
                var parts = text.Split('?');
                if (parts.Length >= 5)
                {
                    var charset = parts[1];
                    var encType = parts[2].ToUpperInvariant();
                    var content = parts[3];

                    if (encType == "B")
                    {
                        var bytes = Convert.FromBase64String(content);
                        var encoding = GetEncodingSafely(charset);
                        return encoding.GetString(bytes).Trim();
                    }
                    else if (encType == "Q")
                    {
                        var decoded = System.Net.WebUtility.UrlDecode(content.Replace("=", "%"));
                        return decoded.Trim();
                    }
                }
            }
            catch { }
        }

        // 3. Explicit base64: prefix (standard in profile-title and announce headers)
        if (text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            string b64 = text.Substring("base64:".Length).Trim();
            if (TryDecodeBase64Text(b64, out var decoded))
            {
                return decoded;
            }
            return b64;
        }

        // 4. URL-encoded UTF-8 string (%D0%9C%D0%BE%D1%8F...)
        if (text.Contains('%'))
        {
            try
            {
                var unescaped = Uri.UnescapeDataString(text);
                if (unescaped != text)
                {
                    var nested = DecodeSubscriptionTitle(unescaped);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
            }
            catch { }
        }

        // 5. Raw Base64 string without any prefix (e.g. 0JzQvtGPINCf0L7QtNC/0LjRgdC60LA= or SGFwcCBTdWJzY3JpcHRpb24=)
        if (TryDecodeBase64Text(text, out var rawDecoded))
        {
            return rawDecoded;
        }

        return text;
    }

    private static Encoding GetEncodingSafely(string charset)
    {
        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    public static bool TryDecodeBase64Text(string input, out string result)
    {
        result = input;
        if (string.IsNullOrWhiteSpace(input) || input.Length < 4)
            return false;

        string trimmed = input.Trim().Replace(" ", "").Replace("\r", "").Replace("\n", "");

        // Only valid Base64 / URL-safe Base64 characters are allowed
        foreach (char c in trimmed)
        {
            bool isB64Char = (c >= 'A' && c <= 'Z') ||
                             (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '+' || c == '/' || c == '=' || c == '-' || c == '_';
            if (!isB64Char)
                return false;
        }

        // URL-safe Base64 normalization
        string normalized = trimmed.Replace('-', '+').Replace('_', '/');
        int pad = normalized.Length % 4;
        if (pad == 2) normalized += "==";
        else if (pad == 3) normalized += "=";
        else if (pad == 1) return false;

        try
        {
            byte[] bytes = Convert.FromBase64String(normalized);
            if (bytes.Length == 0) return false;

            // Strict UTF-8 validation
            var utf8Strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
            string decoded = utf8Strict.GetString(bytes);

            // Reject if it contains binary control characters
            foreach (char c in decoded)
            {
                if (char.IsControl(c) && c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    return false;
            }

            // Distinguish from ordinary ASCII words (e.g. "Server", "Default", "RomaniaWGVI6657"):
            // Base64 encoding produces specific indicators:
            // 1) Contains '=', '+', '/', '-', or '_'
            // 2) Decoded text contains non-ASCII characters (e.g. Cyrillic, Chinese, emojis)
            // 3) Decoded text contains spaces or punctuation while input was a contiguous block
            // 4) Input had digits interspersed
            bool hasB64Markers = trimmed.Contains('=') || trimmed.Contains('+') || trimmed.Contains('/') ||
                                trimmed.Contains('-') || trimmed.Contains('_');
            bool hasRichDecoded = decoded.Any(c => c > 127 || c == ' ' || c == '\t' || c == '\n' || c == '-' || c == '_' || c == '.' || c == ',' || c == '!' || c == '?' || c == ':');
            bool hasDigits = trimmed.Any(char.IsDigit);

            if (!hasB64Markers && !hasRichDecoded && !hasDigits)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(decoded))
            {
                result = decoded.Trim();
                return true;
            }
        }
        catch
        {
            // Invalid base64 or invalid UTF-8 bytes
        }

        return false;
    }

    public static (string trafficInfo, double percent, string expireText) ParseUserInfoHeader(string headerValue)
    {
        long upload = 0;
        long download = 0;
        long total = 0;
        long expire = 0;

        var parts = headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var kv = p.Trim().Split('=', 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim().ToLowerInvariant();
                var val = kv[1].Trim();
                if (long.TryParse(val, out long num))
                {
                    if (key == "upload") upload = num;
                    else if (key == "download") download = num;
                    else if (key == "total") total = num;
                    else if (key == "expire") expire = num;
                }
            }
        }

        long usedBytes = upload + download;
        double usedGb = usedBytes / (1024.0 * 1024.0 * 1024.0);
        double totalGb = total / (1024.0 * 1024.0 * 1024.0);

        string trafficInfo;
        double percent = 0;

        if (total > 0)
        {
            trafficInfo = $"{usedGb:F1}GB / {totalGb:F0}GB";
            percent = Math.Clamp((double)usedBytes / total * 100.0, 0, 100);
        }
        else if (usedBytes > 0)
        {
            trafficInfo = $"{usedGb:F1}GB / ∞";
            percent = 30.0;
        }
        else
        {
            trafficInfo = "0GB / ∞";
            percent = 0.0;
        }

        string expireText;
        bool isRu = LocalizationService.Instance.CurrentLanguage == "ru";
        if (expire > 0)
        {
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(expire).LocalDateTime;
                expireText = isRu ? $"Истекает: {dt:dd.MM.yyyy}" : $"Expires: {dt:dd.MM.yyyy}";
            }
            catch
            {
                expireText = isRu ? "Истекает: Бессрочно" : "Expires: Lifetime";
            }
        }
        else
        {
            expireText = isRu ? "Истекает: Бессрочно" : "Expires: Lifetime";
        }

        return (trafficInfo, percent, expireText);
    }

    private static ServerNode? TryParseUriNode(string uriString, string subscriptionId)
    {
        try
        {
            if (uriString.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseVmess(uriString, subscriptionId);
            }

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                return null;

            string scheme = uri.Scheme.ToLowerInvariant();
            string fragment = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : string.Empty;

            var queryParams = HttpUtility.ParseQueryString(uri.Query);

            string protocol = scheme.ToUpperInvariant();
            string host = uri.Host;
            int port = uri.Port > 0 ? uri.Port : 443;
            string transport = queryParams["type"] ?? queryParams["net"] ?? "TCP";
            string security = queryParams["security"] ?? "TLS";

            string rawName = !string.IsNullOrWhiteSpace(fragment) ? fragment : $"{protocol} {host}:{port}";
            var (emoji, code, country) = CountryFlagHelper.ParseServerFlag(rawName);

            string transportUpper = transport.ToUpperInvariant();
            string securityUpper = security.ToUpperInvariant();
            string transportSecurity = $"{protocol} / {transportUpper} / {securityUpper}";

            if (scheme == "vless")
            {
                if (securityUpper == "REALITY")
                    transportSecurity = "VLESS / TCP / REALITY";
                else if (transportUpper == "WS")
                    transportSecurity = "VLESS / WebSocket / TLS";
            }
            else if (scheme == "hysteria2" || scheme == "hy2")
            {
                protocol = "HY2";
                transportSecurity = "HY2 / QUIC / UDP";
            }
            else if (scheme == "awg" || scheme == "wireguard" || scheme == "wg")
            {
                protocol = scheme == "awg" ? "AWG" : "WG";
                transportSecurity = $"{protocol} / UDP / OBFUSCATED";
            }
            else if (scheme == "trojan")
            {
                protocol = "TROJAN";
                transportSecurity = "TROJAN / TCP / TLS";
            }
            else if (scheme == "ss")
            {
                protocol = "SS";
                transportSecurity = "SHADOWSOCKS / AEAD";
            }

            string cleanName = CountryFlagHelper.CleanServerName(rawName);

            return new ServerNode
            {
                SubscriptionId = subscriptionId,
                Name = cleanName,
                Country = country,
                CountryCode = code,
                FlagEmoji = emoji,
                Host = host,
                Port = port,
                Protocol = protocol,
                TransportInfo = transportUpper,
                SecurityInfo = securityUpper,
                TransportSecurity = transportSecurity,
                RawConfig = uriString,
                PingMs = -1,
                PingSuccess = false,
                HasPinged = false
            };
        }
        catch
        {
            return null;
        }
    }

    private static ServerNode? ParseVmess(string vmessUri, string subscriptionId)
    {
        try
        {
            var base64 = vmessUri.Substring("vmess://".Length).Trim();
            var json = DecodeIfBase64(base64);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string name = root.TryGetProperty("ps", out var psProp) ? psProp.GetString() ?? "" : "VMess Server";
            string host = root.TryGetProperty("add", out var addProp) ? addProp.GetString() ?? "127.0.0.1" : "127.0.0.1";
            int port = 443;
            if (root.TryGetProperty("port", out var portProp))
            {
                if (portProp.ValueKind == JsonValueKind.Number) port = portProp.GetInt32();
                else if (int.TryParse(portProp.GetString(), out int p)) port = p;
            }

            string net = root.TryGetProperty("net", out var netProp) ? netProp.GetString() ?? "tcp" : "tcp";
            string tls = root.TryGetProperty("tls", out var tlsProp) ? tlsProp.GetString() ?? "none" : "none";

            var (emoji, code, country) = CountryFlagHelper.ParseServerFlag(name);

            return new ServerNode
            {
                SubscriptionId = subscriptionId,
                Name = CountryFlagHelper.CleanServerName(name),
                Country = country,
                CountryCode = code,
                FlagEmoji = emoji,
                Host = host,
                Port = port,
                Protocol = "VMESS",
                TransportInfo = net.ToUpperInvariant(),
                SecurityInfo = tls.ToUpperInvariant(),
                TransportSecurity = $"VMESS / {net.ToUpperInvariant()} / {tls.ToUpperInvariant()}",
                RawConfig = vmessUri,
                PingMs = -1,
                PingSuccess = false,
                HasPinged = false
            };
        }
        catch
        {
            return null;
        }
    }

    public static ServerNode? ParseAwgConf(string iniContent, string subscriptionId)
    {
        try
        {
            var awg = AmneziaWgConfig.ParseIni(iniContent);
            string host = "127.0.0.1";
            int port = 51820;

            if (!string.IsNullOrWhiteSpace(awg.Endpoint))
            {
                var lastColon = awg.Endpoint.LastIndexOf(':');
                if (lastColon > 0 && lastColon < awg.Endpoint.Length - 1)
                {
                    host = awg.Endpoint.Substring(0, lastColon).Trim('[', ']', ' ');
                    if (int.TryParse(awg.Endpoint.Substring(lastColon + 1).Trim(), out int p)) port = p;
                }
                else
                {
                    host = awg.Endpoint.Trim();
                }
            }

            string nodeName = !string.IsNullOrWhiteSpace(host) && host != "127.0.0.1"
                ? $"AWG {host}:{port}"
                : "AmneziaWG Server";

            var (flag, code, country) = CountryFlagHelper.ParseServerFlag(nodeName);

            bool isAwg = awg.HasV2Headers || awg.Jc > 0 || awg.S1 > 0 || awg.S2 > 0;
            string proto = isAwg ? "AWG" : "WG";

            return new ServerNode
            {
                SubscriptionId = subscriptionId,
                Name = nodeName,
                Country = country,
                CountryCode = code,
                FlagEmoji = flag,
                Host = host,
                Port = port,
                Protocol = proto,
                TransportInfo = "UDP",
                SecurityInfo = isAwg ? "OBFUSCATED" : "STANDARD",
                TransportSecurity = isAwg ? $"{proto} / UDP / OBFUSCATED" : $"{proto} / UDP / STANDARD",
                RawConfig = iniContent,
                AwgConfig = awg,
                PingMs = -1,
                PingSuccess = false,
                HasPinged = false
            };
        }
        catch
        {
            return null;
        }
    }
}
