# 🌐 lumen aura

> universal cross-platform VPN client built with **Avalonia UI** and **.NET**. unifies legacy tunnels and modern anti-censorship protocols into a single, cohesive interface.

---

## 🚀 installation & quick start
### standalone release (non-root installer)
download the latest release archive from GitHub releases, unpack it, and run the automated installer:

```bash
tar -xzf lumen-aura-linux.tar.gz
cd lumen-aura-linux
./install.sh

```
> this registers `lumen aura` directly in your desktop application launcher (`~/.local/share/applications`) and creates a CLI shortcut in `~/.local/bin/lumen-aura` without requiring root privileges.


### Arch Linux / CachyOS (AUR)
> *coming soon to AUR:*
```bash
yay -S lumen-aura
# or
paru -S lumen-aura

```

### Android (APK)
pre-built APKs are available on the [Releases](https://github.com/pronibgr/lumen-aura/releases) page.

1. download the latest `lumen-aura-arm64-v8a.apk` (or universal build).
2. open the file on your device and confirm the installation.
3. grant the requested **VPN permission** on first launch when prompted.

#### building Android APK from source
requires `.NET 8 SDK` with the `android` workload installed:
```bash
# install Android workload
dotnet workload install android

# build and package APK
dotnet publish Android/aura.Android.csproj -c Release -f net8.0-android

### building from source

requires `dotnet-sdk>=8.0`.
```
---
### 🛠️ launch from source
```bash
# clone repository
git clone [https://github.com/pronibgr/lumen-aura.git](https://github.com/pronibgr/lumen-aura.git)
cd lumen-aura

# restore & build
dotnet restore
dotnet build -c Release

# run desktop client
dotnet run --project src/LumenAura.Desktop

```
---

## 🔄 updating

### ~via AUR~

if installed via your AUR helper, updates are handled automatically alongside system packages:

```bash
yay -Syu
# or
paru -Syu

```

### via standalone installer

simply download the latest release archive, unpack it, and run `install.sh` again — it cleanly updates binaries in-place while keeping your configs intact:

```bash
cd lumen-aura-linux-x64
./install.sh

```

### from source

pull upstream changes and rebuild:

```bash
cd lumen-aura
git pull origin main
dotnet build -c Release

```

---

## ⚡ about

lumen aura tackles routing tool fragmentation by combining classic corporate tunnels and next-gen proxies into a single responsive client.

* 🎯 **platform focus:** actively targeting **Linux** and **Android**. a native Windows build is in progress and will follow in upcoming releases.
* 📦 **packaging:** ready for manual builds on Linux, with an official **AUR** package arriving soon.
* ⚙️ **deep tuning:** flexible configuration engine with routing rules, split-tunneling, and low-level core parameter controls.

---

## 🧪 platform compatibility

| Platform / Distro | Status | Stability | Notes |
| --- | --- | --- | --- |
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

## 🗺️ roadmap

* [x] cross-platform UI base with Avalonia
* [x] Android port (`net8.0-android`)
* [ ] submit PKGBUILD to AUR
* [ ] stabilize Windows routing stack
* [ ] auto-switch proxy profiles based on network changes

