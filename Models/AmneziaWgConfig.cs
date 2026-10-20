using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace aura.Models;

public class AmneziaWgConfig
{
    // Interface section
    public string Address { get; set; } = "10.8.0.2/24";
    public string PrivateKey { get; set; } = string.Empty;
    public string Dns { get; set; } = "1.1.1.1, 8.8.8.8";
    public int Mtu { get; set; } = 1280;

    // AmneziaWG v1 Parameters (Junk Packets)
    public int Jc { get; set; } = 0;        // Junk packet count
    public int Jmin { get; set; } = 0;      // Junk packet min size
    public int Jmax { get; set; } = 0;      // Junk packet max size
    public int S1 { get; set; } = 0;        // Init junk packet size
    public int S2 { get; set; } = 0;        // Response junk packet size
    public int S3 { get; set; } = 0;        // AWG 3.0 cookie packet size
    public int S4 { get; set; } = 0;        // AWG 3.0 data packet size

    // AmneziaWG v2+ Parameters (Custom Magic Headers or Ranges e.g. 7291435-486117520)
    public string H1 { get; set; } = string.Empty; // Handshake initiation header / range
    public string H2 { get; set; } = string.Empty; // Handshake response header / range
    public string H3 { get; set; } = string.Empty; // Cookie reply header / range
    public string H4 { get; set; } = string.Empty; // Transport data header / range

    // AmneziaWG v3 Parameters
    public string I1 { get; set; } = string.Empty;
    public string I2 { get; set; } = string.Empty;
    public string I3 { get; set; } = string.Empty;
    public string HeaderProtectionKey { get; set; } = string.Empty;
    public string ContentPaddingAddition { get; set; } = string.Empty;

    public bool HasV2Headers => !string.IsNullOrWhiteSpace(H1) || !string.IsNullOrWhiteSpace(H2) || !string.IsNullOrWhiteSpace(H3) || !string.IsNullOrWhiteSpace(H4);
    public bool HasV3Params => !string.IsNullOrWhiteSpace(I1) || !string.IsNullOrWhiteSpace(HeaderProtectionKey);

    // Peer section
    public string PublicKey { get; set; } = string.Empty;
    public string PresharedKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "127.0.0.1:51820";
    public string AllowedIPs { get; set; } = "0.0.0.0/0, ::/0";
    public int PersistentKeepalive { get; set; } = 25;

    public string DetectedVersion
    {
        get
        {
            if (HasV3Params)
                return "AWG v3.0 (Header Protection + Junk + Ranges)";
            if (HasV2Headers)
                return "AWG v2.0 (Custom Headers H1-H4 + Junk)";
            if (Jc > 0 || S1 > 0 || S2 > 0)
                return "AWG v1.0 (Junk Packets)";
            return "Standard WireGuard (WG)";
        }
    }

    public static AmneziaWgConfig ParseIni(string iniContent)
    {
        var config = new AmneziaWgConfig();
        if (string.IsNullOrWhiteSpace(iniContent)) return config;

        var lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string currentSection = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim().ToLowerInvariant();
            var val = parts[1].Trim();

            if (currentSection == "interface")
            {
                switch (key)
                {
                    case "address": config.Address = val; break;
                    case "privatekey": config.PrivateKey = val; break;
                    case "dns": config.Dns = val; break;
                    case "mtu": if (int.TryParse(val, out int mtu)) config.Mtu = mtu; break;
                    case "jc": if (int.TryParse(val, out int jc)) config.Jc = jc; break;
                    case "jmin": if (int.TryParse(val, out int jmin)) config.Jmin = jmin; break;
                    case "jmax": if (int.TryParse(val, out int jmax)) config.Jmax = jmax; break;
                    case "s1": if (int.TryParse(val, out int s1)) config.S1 = s1; break;
                    case "s2": if (int.TryParse(val, out int s2)) config.S2 = s2; break;
                    case "s3": if (int.TryParse(val, out int s3)) config.S3 = s3; break;
                    case "s4": if (int.TryParse(val, out int s4)) config.S4 = s4; break;
                    case "h1": config.H1 = val; break;
                    case "h2": config.H2 = val; break;
                    case "h3": config.H3 = val; break;
                    case "h4": config.H4 = val; break;
                    case "i1": config.I1 = val; break;
                    case "i2": config.I2 = val; break;
                    case "i3": config.I3 = val; break;
                    case "headerprotectionkey": config.HeaderProtectionKey = val; break;
                    case "contentpaddingaddition": config.ContentPaddingAddition = val; break;
                }
            }
            else if (currentSection == "peer")
            {
                switch (key)
                {
                    case "publickey": config.PublicKey = val; break;
                    case "presharedkey": config.PresharedKey = val; break;
                    case "endpoint": config.Endpoint = val; break;
                    case "allowedips": config.AllowedIPs = val; break;
                    case "persistentkeepalive":
                        if (int.TryParse(val, out int pka)) config.PersistentKeepalive = pka;
                        break;
                }
            }
        }

        return config;
    }
}
