# 🌐 lumen aura

> universal cross-platform VPN client built with **Avalonia UI** and **.NET**. unifies legacy tunnels and modern anti-censorship protocols into a single, cohesive interface.

---

## ⚡ about

lumen aura tackles routing tool fragmentation by combining classic corporate tunnels and next-gen proxies into a single responsive client.

* 🎯 **platform focus:** actively targeting **Linux** and **Android**. a native Windows build is in progress and will follow in upcoming releases.
* 📦 **packaging:** ready for manual builds on Linux, with an official **AUR** package arriving soon.
* ⚙️ **deep tuning:** flexible configuration engine with routing rules, split-tunneling, and low-level core parameter controls.

---

## 🧪 platform compatibility

| Platform / Distro | Status | Stability | Notes |
| :--- | :---: | :---: | :--- |
| **CachyOS** | 🟢 working | ⭐⭐⭐⭐⭐ **flawless** | native execution, lowest latency, best overall performance |
| **Arch Linux** | 🟢 working | ⭐⭐⭐⭐⭐ **great** | fully supported dependencies, AUR package in progress |
| **Fedora** | 🟢 working | ⭐⭐⭐⭐⭐ **great** | solid stability on current .NET runtime and Wayland / X11 |
| **Linux Mint** | 🟡 working | ⭐⭐⭐ | functional, but older system libraries may need attention |
| **Android** | 🟢 supported | ⭐⭐⭐⭐ | tailored `SingleView` UI optimized for touch screens |
| **Windows** | 🟠 experimental | ⭐⭐ | compiles cleanly, but platform-specific routing logic is WIP |

> ℹ️ *note on Windows:* builds fine under `win-x64`, but active development is centered on unix-like systems. virtual adapter and routing integrations still need fine-tuning.

---

## 🛡️ supported protocols & technologies

designed as an all-in-one client handling modern censorship bypass and enterprise tunnels:

### next-gen / anti-censorship
* **VLESS** (**REALITY**, **Vision**)
* **AmneziaWG** (**2.0-3.0**)
* **Hysteria 2**
* **TUIC v5**
* **Shadowsocks 2022**
* **ShadowTLS**
* **NaiveProxy**
* **VMess**
* **Trojan**

### classic & enterprise VPN
* **WireGuard**
* **OpenVPN**
* **IKEv2 / IPsec**
* **SSTP**
* **SoftEther**
* *...and other standard config formats.*

---

## 🚀 installation & build

### Arch Linux / CachyOS (AUR)
> *coming soon to AUR:*
```bash
yay -S lumen-aura
# or
paru -S lumen-aura