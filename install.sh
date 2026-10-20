#!/usr/bin/env bash
set -e

# ==============================================================================
# lumen aura - Non-root Linux User-Space Installer
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Color codes
if [ -t 1 ]; then
    GREEN='\033[0;32m'
    BLUE='\033[0;34m'
    YELLOW='\033[1;33m'
    RED='\033[0;31m'
    BOLD='\033[1m'
    NC='\033[0m'
else
    GREEN=''
    BLUE=''
    YELLOW=''
    RED=''
    BOLD=''
    NC=''
fi

INFO="${BLUE}[INFO]${NC}"
SUCCESS="${GREEN}[SUCCESS]${NC}"
WARN="${YELLOW}[WARN]${NC}"
ERROR="${RED}[ERROR]${NC}"

echo -e "${BOLD}Installing lumen aura...${NC}"

# 1. Privilege Check
if [ "$(id -u)" -eq 0 ]; then
    echo -e "${WARN} You are running this installer as root (UID 0)."
    echo -e "${WARN} This installer is designed for non-root user space (~/.local)."
    if [ -t 0 ]; then
        read -r -p "Do you want to continue and install into /root/.local? [y/N]: " confirm
        case "$confirm" in
            [yY][eE][sS]|[yY])
                echo -e "${INFO} Continuing root user-space installation..."
                ;;
            *)
                echo -e "${ERROR} Installation aborted by user."
                exit 1
                ;;
        esac
    else
        echo -e "${WARN} Non-interactive execution as root. Proceeding with user space under $HOME/.local..."
    fi
fi

# 2. Target Locations
LOCAL_SHARE="${XDG_DATA_HOME:-$HOME/.local/share}"
LOCAL_BIN="$HOME/.local/bin"
TARGET_DIR="$LOCAL_SHARE/lumen-aura"
APPLICATIONS_DIR="$LOCAL_SHARE/applications"
HICOLOR_DIR="$LOCAL_SHARE/icons/hicolor/256x256/apps"
HICOLOR_128_DIR="$LOCAL_SHARE/icons/hicolor/128x128/apps"
PIXMAPS_DIR="$LOCAL_SHARE/pixmaps"

# 3. Payload Discovery & Validation
PAYLOAD_DIR=""
BINARY_NAME=""

# Check possible locations for the executable
if [ -f "$SCRIPT_DIR/LumenAura.Desktop" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR"
    BINARY_NAME="LumenAura.Desktop"
elif [ -f "$SCRIPT_DIR/aura" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR"
    BINARY_NAME="aura"
elif [ -f "$SCRIPT_DIR/lumen-aura" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR"
    BINARY_NAME="lumen-aura"
elif [ -f "$SCRIPT_DIR/bin/Release/net10.0/linux-x64/publish/aura" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR/bin/Release/net10.0/linux-x64/publish"
    BINARY_NAME="aura"
elif [ -f "$SCRIPT_DIR/bin/Release/net10.0/linux-x64/publish/LumenAura.Desktop" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR/bin/Release/net10.0/linux-x64/publish"
    BINARY_NAME="LumenAura.Desktop"
elif [ -f "$SCRIPT_DIR/publish/aura" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR/publish"
    BINARY_NAME="aura"
elif [ -f "$SCRIPT_DIR/publish/LumenAura.Desktop" ]; then
    PAYLOAD_DIR="$SCRIPT_DIR/publish"
    BINARY_NAME="LumenAura.Desktop"
fi

if [ -z "$PAYLOAD_DIR" ] || [ -z "$BINARY_NAME" ]; then
    echo -e "${ERROR} Binary not found. Please ensure you extracted the full release archive."
    exit 1
fi

echo -e "${INFO} Found application payload at: $PAYLOAD_DIR (binary: $BINARY_NAME)"

# 4. Disk Space Check (~50MB required)
REQUIRED_KB=51200
mkdir -p "$LOCAL_SHARE"
AVAILABLE_KB=$(df -Pk "$LOCAL_SHARE" 2>/dev/null | awk 'NR==2 {print $4}')
if [ -n "$AVAILABLE_KB" ] && [ "$AVAILABLE_KB" -lt "$REQUIRED_KB" ]; then
    echo -e "${ERROR} Insufficient disk space in $LOCAL_SHARE. Required: ~50MB, Available: $((AVAILABLE_KB / 1024))MB."
    exit 1
fi

# 5. Directory Safety & Preparation
mkdir -p "$TARGET_DIR"
mkdir -p "$LOCAL_BIN"
mkdir -p "$APPLICATIONS_DIR"

# 6. Copy Application Payload
echo -e "${INFO} Deploying application files to $TARGET_DIR..."
if [ "$PAYLOAD_DIR" = "$SCRIPT_DIR" ]; then
    # Copy files excluding installer/scripts/git/aur
    find "$PAYLOAD_DIR" -maxdepth 1 \
        ! -name "$(basename "$SCRIPT_DIR")" \
        ! -name "install.sh" \
        ! -name "uninstall.sh" \
        ! -name ".git" \
        ! -name "aur" \
        ! -name "obj" \
        ! -name "bin" \
        -exec cp -r -t "$TARGET_DIR" {} +
else
    cp -r "$PAYLOAD_DIR/"* "$TARGET_DIR/"
fi

# Ensure executable permissions and compatibility symlink
chmod 755 "$TARGET_DIR/$BINARY_NAME"
if [ "$BINARY_NAME" != "LumenAura.Desktop" ]; then
    ln -sf "$BINARY_NAME" "$TARGET_DIR/LumenAura.Desktop"
fi
if [ "$BINARY_NAME" != "aura" ]; then
    ln -sf "$BINARY_NAME" "$TARGET_DIR/aura"
fi

# 7. Create CLI Symlink
CLI_SYMLINK="$LOCAL_BIN/lumen-aura"
ln -sf "$TARGET_DIR/LumenAura.Desktop" "$CLI_SYMLINK"
ln -sf "$TARGET_DIR/LumenAura.Desktop" "$LOCAL_BIN/aura"
chmod 755 "$CLI_SYMLINK"
echo -e "${SUCCESS} Created CLI symlink: $CLI_SYMLINK -> $TARGET_DIR/LumenAura.Desktop"

# 8. Install Application Icon
ICON_SRC=""
if [ -f "$SCRIPT_DIR/Assets/aura.png" ]; then
    ICON_SRC="$SCRIPT_DIR/Assets/aura.png"
elif [ -f "$SCRIPT_DIR/aura.png" ]; then
    ICON_SRC="$SCRIPT_DIR/aura.png"
elif [ -f "$SCRIPT_DIR/Assets/lumen-aura.png" ]; then
    ICON_SRC="$SCRIPT_DIR/Assets/lumen-aura.png"
elif [ -f "$SCRIPT_DIR/lumen-aura.png" ]; then
    ICON_SRC="$SCRIPT_DIR/lumen-aura.png"
fi

if [ -n "$ICON_SRC" ]; then
    mkdir -p "$HICOLOR_DIR" "$HICOLOR_128_DIR" "$PIXMAPS_DIR"
    cp "$ICON_SRC" "$HICOLOR_DIR/lumen-aura.png"
    cp "$ICON_SRC" "$HICOLOR_128_DIR/lumen-aura.png"
    cp "$ICON_SRC" "$PIXMAPS_DIR/lumen-aura.png"
    chmod 644 "$HICOLOR_DIR/lumen-aura.png" "$HICOLOR_128_DIR/lumen-aura.png" "$PIXMAPS_DIR/lumen-aura.png"
    echo -e "${SUCCESS} Installed desktop icons."
else
    echo -e "${WARN} Icon asset not found; continuing without updating icon files."
fi

# 9. Create Desktop Entry
DESKTOP_FILE="$APPLICATIONS_DIR/lumen-aura.desktop"
cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=lumen aura
GenericName=VPN and Proxy Client
Comment=Universal cross-platform VPN and proxy client in Material 3 Expressive design
Exec=$CLI_SYMLINK %u
Icon=lumen-aura
Terminal=false
Categories=Utility;Network;VPN;
StartupWMClass=aura
EOF

chmod 644 "$DESKTOP_FILE"
echo -e "${SUCCESS} Created desktop launcher entry at $DESKTOP_FILE"

# 10. Update Desktop & Icon Databases
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APPLICATIONS_DIR" >/dev/null 2>&1 || true
    echo -e "${INFO} Desktop application database updated."
fi

if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f "$LOCAL_SHARE/icons/hicolor" >/dev/null 2>&1 || true
fi

# 11. Check PATH
case ":$PATH:" in
    *":$LOCAL_BIN:"*)
        echo -e "${INFO} '$LOCAL_BIN' is already in your PATH."
        ;;
    *)
        echo -e "${WARN} '$LOCAL_BIN' is not currently in your PATH environment variable."
        echo -e "${WARN} To launch 'lumen-aura' directly from any terminal, add this line to your ~/.bashrc or ~/.zshrc:"
        echo -e "       export PATH=\"\$HOME/.local/bin:\$PATH\""
        ;;
esac

echo -e "\n${SUCCESS} ${BOLD}lumen aura has been successfully installed!${NC}"
echo -e "${INFO} You can now start the application via your desktop launcher or run: ${BOLD}lumen-aura${NC}\n"
