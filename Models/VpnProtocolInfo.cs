using System;
using System.Collections.Generic;

namespace aura.Models;

public enum ProtocolCategory
{
    ClassicVpn,
    ModernProxy,
    AdditionalTech,
    TransportAndCore
}

public class VpnProtocolInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public ProtocolCategory Category { get; set; }
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;
    public string Transport { get; set; } = "TCP/UDP";
    public string Obfuscation { get; set; } = "None";
    public string BadgeColor { get; set; } = "#7c3aed";
    public bool SupportsUdp { get; set; } = true;
    public bool DpiResistant { get; set; } = true;
    public List<string> KeyFeatures { get; set; } = new();

    public static List<VpnProtocolInfo> GetAllSupportedProtocols()
    {
        return new List<VpnProtocolInfo>
        {
            // Классические VPN
            new() {
                Id = "wireguard",
                Name = "WireGuard",
                ShortName = "WG",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Extremely simple, high-speed, modern cryptographic VPN tunneling protocol.",
                DescriptionRu = "Крайне быстрый, легковесный современный криптографический VPN протокол.",
                Transport = "UDP",
                Obfuscation = "None (Easily blocked by DPI)",
                BadgeColor = "#2563eb",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "ChaCha20-Poly1305", "Noise protocol framework", "Instant handshake", "Kernel-level performance" }
            },
            new() {
                Id = "amneziawg",
                Name = "AmneziaWG",
                ShortName = "AWG",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "WireGuard fork with advanced DPI bypass: custom header magic (H1-H4) and junk packets (Jc, Jmin, Jmax, S1, S2). Supported for ALL versions.",
                DescriptionRu = "Модификация WireGuard для обхода блокировок DPI: маскировка заголовков (H1-H4) и мусорные пакеты (Jc, Jmin, Jmax, S1, S2). Полная поддержка ЛЮБЫХ версий AWG.",
                Transport = "UDP (Obfuscated)",
                Obfuscation = "Junk packets (Jc/Jmin/Jmax/S1/S2) & Headers (H1-H4)",
                BadgeColor = "#8b5cf6",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Junk packet injection (Jc)", "Random packet sizing (Jmin-Jmax)", "Initiation/Response padding (S1, S2)", "Custom magic headers (H1-H4)", "Full WireGuard speed" }
            },
            new() {
                Id = "openvpn",
                Name = "OpenVPN",
                ShortName = "OVPN",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Industry standard, highly configurable SSL/TLS VPN protocol with TCP/UDP support.",
                DescriptionRu = "Классический проверенный временем SSL/TLS VPN протокол с поддержкой UDP и TCP.",
                Transport = "UDP / TCP",
                Obfuscation = "tls-crypt / tls-auth / Scramble",
                BadgeColor = "#ea580c",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "OpenSSL ciphers", "TAP/TUN devices", "Certificate & user/pass auth", "Custom routing pushed by server" }
            },
            new() {
                Id = "ikev2",
                Name = "IKEv2/IPsec",
                ShortName = "IKEv2",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Fast and reliable protocol with native support on mobile and desktop OS, excellent mobility reconnect.",
                DescriptionRu = "Быстрый и стабильный протокол с нативной поддержкой в мобильных и десктопных ОС.",
                Transport = "UDP (500, 4500)",
                Obfuscation = "Standard IPsec",
                BadgeColor = "#0284c7",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "MOBIKE protocol", "Diffie-Hellman groups", "AES-GCM", "Native OS stack" }
            },
            new() {
                Id = "ipsec",
                Name = "IPsec",
                ShortName = "IPsec",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Network layer security protocol suite providing authentication and encryption of IP packets.",
                DescriptionRu = "Набор протоколов сетевого уровня для аутентификации и шифрования IP-пакетов.",
                Transport = "ESP / AH / UDP",
                Obfuscation = "None",
                BadgeColor = "#0369a1",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "Network layer (L3)", "Phase 1 & Phase 2 SA", "Hardware crypto acceleration" }
            },
            new() {
                Id = "l2tp",
                Name = "L2TP/IPsec",
                ShortName = "L2TP",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Layer 2 Tunneling Protocol combined with IPsec for encryption and mutual authentication.",
                DescriptionRu = "Туннельный протокол канального уровня в связке с IPsec для шифрования.",
                Transport = "UDP (1701)",
                Obfuscation = "None",
                BadgeColor = "#4f46e5",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "Pre-shared key (PSK)", "PPP framing", "Wide device compatibility" }
            },
            new() {
                Id = "sstp",
                Name = "SSTP",
                ShortName = "SSTP",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Secure Socket Tunneling Protocol transmitting PPP traffic through an SSL 3.0/TLS channel over port 443.",
                DescriptionRu = "Протокол защищенного туннелирования сокетов, передающий PPP-трафик по HTTPS порту 443.",
                Transport = "TCP (443)",
                Obfuscation = "HTTPS/TLS encapsulation",
                BadgeColor = "#059669",
                SupportsUdp = false,
                DpiResistant = true,
                KeyFeatures = new() { "Bypasses strict firewalls", "Port 443 default", "Native Windows client support", "TLS 1.3 handshake" }
            },
            new() {
                Id = "pptp",
                Name = "PPTP",
                ShortName = "PPTP",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Legacy Point-to-Point Tunneling Protocol. Fast but considered cryptographically obsolete.",
                DescriptionRu = "Устаревший протокол туннелирования точка-точка. Быстрый, но слабый криптографически.",
                Transport = "TCP (1723) + GRE",
                Obfuscation = "None",
                BadgeColor = "#6b7280",
                SupportsUdp = false,
                DpiResistant = false,
                KeyFeatures = new() { "Legacy compatibility", "MPPE encryption", "Low CPU overhead" }
            },
            new() {
                Id = "softether",
                Name = "SoftEther VPN",
                ShortName = "SoftEther",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "Multi-protocol VPN software with ultra-fast Ethernet-over-HTTPS protocol.",
                DescriptionRu = "Мощная многопротокольная система со скоростным протоколом Ethernet-over-HTTPS.",
                Transport = "TCP / HTTPS",
                Obfuscation = "Ethernet-over-HTTPS",
                BadgeColor = "#10b981",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Virtual HUB", "NAT traversal", "Parallel TCP connections", "High throughput" }
            },
            new() {
                Id = "openconnect",
                Name = "OpenConnect",
                ShortName = "OpenConnect",
                Category = ProtocolCategory.ClassicVpn,
                DescriptionEn = "SSL VPN client compatible with Cisco AnyConnect and Juniper Pulse Connect Secure.",
                DescriptionRu = "Клиент SSL VPN, полностью совместимый с протоколами Cisco AnyConnect и Pulse Secure.",
                Transport = "TCP (TLS) + UDP (DTLS)",
                Obfuscation = "Cisco AnyConnect mimicry",
                BadgeColor = "#0d9488",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "DTLS acceleration", "AnyConnect cookie auth", "Corporate enterprise standard" }
            },

            // Современные прокси-протоколы
            new() {
                Id = "vless",
                Name = "VLESS",
                ShortName = "VLESS",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Lightweight, stateless proxy protocol without double encryption, paired with REALITY / XTLS-Vision.",
                DescriptionRu = "Легковесный протокол без лишнего шифрования в связке с технологиями REALITY и Vision.",
                Transport = "TCP / gRPC / WebSocket / XHTTP",
                Obfuscation = "REALITY / TLS Fingerprinting",
                BadgeColor = "#8b5cf6",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "XTLS-Vision flow", "REALITY masquerade", "No server-side decryption delay", "uTLS fingerprint spoofing" }
            },
            new() {
                Id = "vmess",
                Name = "VMess",
                ShortName = "VMess",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Original encrypted transmission protocol designed for V2Ray with multiple security ciphers.",
                DescriptionRu = "Оригинальный зашифрованный протокол экосистемы V2Ray с динамическими портами.",
                Transport = "TCP / mKCP / WebSocket / gRPC",
                Obfuscation = "TLS / WS / mKCP Header",
                BadgeColor = "#ec4899",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "AEAD authentication", "Dynamic port hopping", "Multi-hop routing", "AlterID support" }
            },
            new() {
                Id = "trojan",
                Name = "Trojan",
                ShortName = "Trojan",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Mimics normal HTTPS traffic indistinguishably by wrapping traffic in standard TLS to legitimate domains.",
                DescriptionRu = "Маскирует трафик под обычный HTTPS трафик веб-сайта через валидный TLS сертификат.",
                Transport = "TCP / WebSocket / gRPC",
                Obfuscation = "Standard HTTPS TLS",
                BadgeColor = "#f43f5e",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Perfect HTTPS mimicry", "Password hash auth", "ALPN negotiation (h2, http/1.1)" }
            },
            new() {
                Id = "shadowsocks",
                Name = "Shadowsocks",
                ShortName = "SS",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Classic encrypted SOCKS5 proxy protocol using AEAD ciphers (AES-256-GCM, ChaCha20-Poly1305).",
                DescriptionRu = "Классический протокол безопасного шифрованного прокси с шифрами AEAD.",
                Transport = "TCP / UDP",
                Obfuscation = "AEAD / SIP003 Plugins",
                BadgeColor = "#6366f1",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "AEAD ciphers", "Minimal latency", "Plugin architecture (v2ray-plugin, obfs)" }
            },
            new() {
                Id = "shadowsocks2022",
                Name = "Shadowsocks 2022",
                ShortName = "SS-2022",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Next-gen Shadowsocks protocol with BLAKE3 key derivation, session subkeys and replay protection.",
                DescriptionRu = "Новое поколение Shadowsocks с защитой от повторов (Replay) и ключами BLAKE3.",
                Transport = "TCP / UDP",
                Obfuscation = "2022 AEAD Specs",
                BadgeColor = "#4338ca",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "2022-blake3-aes-128/256-gcm", "Fixed header size", "Replay filter cache", "Multi-user subkeys" }
            },
            new() {
                Id = "hysteria",
                Name = "Hysteria",
                ShortName = "HY1",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Custom UDP protocol based on QUIC, customized for maximizing speed over lossy network links.",
                DescriptionRu = "Кастомный протокол на базе QUIC для максимальной скорости на проблемных каналах.",
                Transport = "UDP (QUIC-based)",
                Obfuscation = "Protocol Obfuscation",
                BadgeColor = "#06b6d4",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Brutal congestion control", "Loss tolerant", "Bandwidth throttling bypass" }
            },
            new() {
                Id = "hysteria2",
                Name = "Hysteria 2",
                ShortName = "HY2",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Complete rewrite of Hysteria with native QUIC stack, Salamander obfuscation and port hopping.",
                DescriptionRu = "Полностью переработанный Hysteria 2: нативный QUIC, обфускация Salamander и Port Hopping.",
                Transport = "UDP (Custom QUIC)",
                Obfuscation = "Salamander / Standard TLS",
                BadgeColor = "#0ea5e9",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Salamander password obfs", "Port hopping range", "BBR congestion", "Zero RTT connection" }
            },
            new() {
                Id = "tuic",
                Name = "TUIC",
                ShortName = "TUIC",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Delicately designed proxy protocol over native QUIC 0-RTT with concurrent multiplexing.",
                DescriptionRu = "Изящный протокол поверх нативного QUIC с 0-RTT рукопожатием и низким оверхедом.",
                Transport = "UDP (QUIC)",
                Obfuscation = "QUIC Handshake Mask",
                BadgeColor = "#14b8a6",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "QUIC v5", "0-RTT connection resumption", "Native UDP relay", "BBR / Cubic control" }
            },
            new() {
                Id = "naiveproxy",
                Name = "NaiveProxy",
                ShortName = "Naive",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Uses Chrome's native network stack to camouflage proxy traffic as genuine Chrome HTTPS traffic.",
                DescriptionRu = "Использует настоящий сетевой стек браузера Chrome для идеальной маскировки под HTTPS.",
                Transport = "TCP / HTTP/2 / HTTP/3",
                Obfuscation = "Genuine Chrome TLS Fingerprint",
                BadgeColor = "#3b82f6",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Chromium network stack", "Uchuu padding", "Zero distinctive DPI signatures" }
            },
            new() {
                Id = "anytls",
                Name = "AnyTLS",
                ShortName = "AnyTLS",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "TLS camouflage layer that intercepts and wraps payloads into standards-compliant TLS records.",
                DescriptionRu = "Слой маскировки, упаковывающий трафик в полностью валидные TLS-записи.",
                Transport = "TCP (TLS)",
                Obfuscation = "Dynamic TLS Records",
                BadgeColor = "#a855f7",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "TLS record fragmentation", "ALPN spoofing", "Anti-active probing" }
            },
            new() {
                Id = "shadowtls",
                Name = "ShadowTLS",
                ShortName = "ShadowTLS",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Transparently forwards TLS handshakes to genuine websites, preventing active probing attacks.",
                DescriptionRu = "Проксирует начальный TLS handshake на реальный доверенный сайт для защиты от зондирования.",
                Transport = "TCP",
                Obfuscation = "Legitimate Server SNI Proxying",
                BadgeColor = "#d946ef",
                SupportsUdp = false,
                DpiResistant = true,
                KeyFeatures = new() { "ShadowTLS v2/v3", "Zero server certificate required", "Active probe immune" }
            },
            new() {
                Id = "snell",
                Name = "Snell",
                ShortName = "Snell",
                Category = ProtocolCategory.ModernProxy,
                DescriptionEn = "Extremely fast, lightweight proxy protocol with HTTP/TLS obfuscation headers.",
                DescriptionRu = "Сверхбыстрый прокси-протокол с встроенной HTTP/TLS обфускацией заголовков.",
                Transport = "TCP",
                Obfuscation = "HTTP / TLS Obfs",
                BadgeColor = "#f59e0b",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Snell v3/v4", "PSK authentication", "Chacha20/AES", "Low memory footprint" }
            },

            // Дополнительные технологии
            new() {
                Id = "cloak",
                Name = "Cloak",
                ShortName = "Cloak",
                Category = ProtocolCategory.AdditionalTech,
                DescriptionEn = "Pluggable transport that obfuscates OpenVPN/Shadowsocks into legitimate TLS web browsing.",
                DescriptionRu = "Плагин маскировки для OpenVPN и Shadowsocks под реальный веб-серфинг.",
                Transport = "TCP (HTTPS)",
                Obfuscation = "Fake Web Page / TLS Camouflage",
                BadgeColor = "#78716c",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Multiplexing", "Anti-probing redirection", "Web server proxying" }
            },
            new() {
                Id = "lightway",
                Name = "Lightway",
                ShortName = "Lightway",
                Category = ProtocolCategory.AdditionalTech,
                DescriptionEn = "High performance modern VPN protocol engineered with WolfSSL for instant connections.",
                DescriptionRu = "Высокоскоростной современный протокол на базе WolfSSL для мгновенного подключения.",
                Transport = "UDP / TCP",
                Obfuscation = "WolfSSL Encryption",
                BadgeColor = "#ef4444",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Instant resume on network drop", "WolfSSL crypto", "Low battery usage" }
            },
            new() {
                Id = "nordlynx",
                Name = "NordLynx",
                ShortName = "NordLynx",
                Category = ProtocolCategory.AdditionalTech,
                DescriptionEn = "Proprietary double NAT architecture built on top of high-speed WireGuard core.",
                DescriptionRu = "Архитектура с двойным NAT на скоростной основе протокола WireGuard.",
                Transport = "UDP",
                Obfuscation = "WireGuard base",
                BadgeColor = "#3b82f6",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "Double NAT system", "Zero identifiable logs", "WireGuard performance" }
            },
            new() {
                Id = "socks5",
                Name = "SOCKS5",
                ShortName = "SOCKS5",
                Category = ProtocolCategory.AdditionalTech,
                DescriptionEn = "Standard proxy protocol for TCP/UDP traffic with user/password authentication support.",
                DescriptionRu = "Стандартный интернет-протокол проксирования трафика с поддержкой авторизации.",
                Transport = "TCP / UDP",
                Obfuscation = "None",
                BadgeColor = "#64748b",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "RFC 1928 compliance", "UDP Associate", "Username/Password auth" }
            },
            new() {
                Id = "ssh",
                Name = "SSH Tunnel",
                ShortName = "SSH",
                Category = ProtocolCategory.AdditionalTech,
                DescriptionEn = "Secure Shell dynamic port forwarding (-D) encrypting arbitrary TCP connections.",
                DescriptionRu = "Динамическое туннелирование портов через протокол Secure Shell (SSH -D).",
                Transport = "TCP (22)",
                Obfuscation = "Standard SSH protocol",
                BadgeColor = "#0f766e",
                SupportsUdp = false,
                DpiResistant = true,
                KeyFeatures = new() { "Public key authentication", "Standard server availability", "Encrypted SOCKS interface" }
            },
            new() {
                Id = "tcp",
                Name = "TCP Transport",
                ShortName = "TCP",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Fundamental reliable connection-oriented transport protocol.",
                DescriptionRu = "Базовый надёжный транспортный протокол с гарантированной доставкой.",
                Transport = "TCP",
                Obfuscation = "Raw / Fake HTTP Header",
                BadgeColor = "#334155",
                SupportsUdp = false,
                DpiResistant = false,
                KeyFeatures = new() { "Flow control", "HTTP header obfuscation", "Standard firewall pass" }
            },
            new() {
                Id = "udp",
                Name = "UDP Transport",
                ShortName = "UDP",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Connectionless transport protocol ideal for real-time traffic, gaming and WireGuard.",
                DescriptionRu = "Транспортный протокол без установления соединения для максимальной скорости и игр.",
                Transport = "UDP",
                Obfuscation = "Raw Datagrams",
                BadgeColor = "#475569",
                SupportsUdp = true,
                DpiResistant = false,
                KeyFeatures = new() { "Lowest packet latency", "Datagram streaming", "Zero handshake delay" }
            },
            new() {
                Id = "quic",
                Name = "QUIC Transport",
                ShortName = "QUIC",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Multiplexed transport protocol built on top of UDP with integrated TLS 1.3 encryption.",
                DescriptionRu = "Мультиплексированный транспортный протокол поверх UDP со встроенным TLS 1.3.",
                Transport = "UDP (QUIC)",
                Obfuscation = "TLS 1.3 Packet Envelope",
                BadgeColor = "#0284c7",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Zero head-of-line blocking", "0-RTT connection", "Connection migration" }
            },

            // Дополнительные механизмы и транспорты Xray / sing-box
            new() {
                Id = "reality",
                Name = "REALITY",
                ShortName = "REALITY",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Xray mechanism that completely eliminates TLS certificate characteristics by borrowing third-party domains.",
                DescriptionRu = "Механизм защиты Xray, устраняющий подозрительные свойства TLS за счет заимствования сертификата чужого домена.",
                Transport = "TCP / gRPC / XHTTP",
                Obfuscation = "Stolen Server Certificate & uTLS",
                BadgeColor = "#10b981",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Public key (pbk)", "Short ID (sid)", "SpiderX crawling", "Zero server domain cost" }
            },
            new() {
                Id = "xhttp",
                Name = "XHTTP",
                ShortName = "XHTTP",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Modern Xray transport protocol supporting streaming, chunked uploads/downloads and CDN acceleration.",
                DescriptionRu = "Современный транспорт Xray с поддержкой потоковой передачи и CDN-совместимости.",
                Transport = "HTTP/2, HTTP/3, XHTTP",
                Obfuscation = "HTTP Chunked Streams",
                BadgeColor = "#8b5cf6",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "packet-up mode", "chunked-down mode", "CDN compatibility" }
            },
            new() {
                Id = "grpc",
                Name = "gRPC",
                ShortName = "gRPC",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Google Remote Procedure Call transport running over HTTP/2 with high-performance multiplexing.",
                DescriptionRu = "Транспорт поверх HTTP/2 с высокой производительностью и эффективным мультиплексированием.",
                Transport = "HTTP/2",
                Obfuscation = "Standard gRPC Services",
                BadgeColor = "#06b6d4",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "MultiMode support", "Custom ServiceName", "H2 multiplexing" }
            },
            new() {
                Id = "websocket",
                Name = "WebSocket (WS)",
                ShortName = "WS",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Transfers proxy traffic encapsulated inside HTTP/1.1 WebSocket frames via reverse proxies and CDNs.",
                DescriptionRu = "Передача трафика внутри WebSocket фреймов через веб-серверы и Cloudflare CDN.",
                Transport = "TCP (HTTP/1.1 Upgrade)",
                Obfuscation = "Standard WebSocket Upgrade",
                BadgeColor = "#eab308",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "CDN proxying (Cloudflare)", "Custom path and headers", "Early data support" }
            },
            new() {
                Id = "mkcp",
                Name = "mKCP",
                ShortName = "mKCP",
                Category = ProtocolCategory.TransportAndCore,
                DescriptionEn = "Stream protocol based on KCP over UDP with header pseudo-camouflage (wechat-video, srtp, dtls).",
                DescriptionRu = "Потоковый протокол на базе KCP поверх UDP с маскировкой заголовков (wechat-video, dtls).",
                Transport = "UDP",
                Obfuscation = "Header type mimicry",
                BadgeColor = "#f97316",
                SupportsUdp = true,
                DpiResistant = true,
                KeyFeatures = new() { "Fast retransmission", "Congestion control", "Custom pseudo-headers" }
            }
        };
    }
}
