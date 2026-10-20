#!/usr/bin/env bash
set -e

# ==============================================================================
# lumen aura - Non-root Linux User-Space Uninstaller
# ==============================================================================

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

echo -e "${BOLD}Uninstalling lumen aura...${NC}"

# Target Locations
LOCAL_SHARE="${XDG_DATA_HOME:-$HOME/.local/share}"
LOCAL_BIN="$HOME/.local/bin"
TARGET_DIR="$LOCAL_SHARE/lumen-aura"
APPLICATIONS_DIR="$LOCAL_SHARE/applications"
HICOLOR_DIR="$LOCAL_SHARE/icons/hicolor/256x256/apps"
HICOLOR_128_DIR="$LOCAL_SHARE/icons/hicolor/128x128/apps"
PIXMAPS_DIR="$LOCAL_SHARE/pixmaps"

REMOVED_ANY=0

# 1. Remove Desktop Entry
if [ -f "$APPLICATIONS_DIR/lumen-aura.desktop" ]; then
    rm -f "$APPLICATIONS_DIR/lumen-aura.desktop"
    echo -e "${SUCCESS} Removed desktop entry: $APPLICATIONS_DIR/lumen-aura.desktop"
    REMOVED_ANY=1
fi

# 2. Remove CLI Symlinks
if [ -L "$LOCAL_BIN/lumen-aura" ] || [ -f "$LOCAL_BIN/lumen-aura" ]; then
    rm -f "$LOCAL_BIN/lumen-aura"
    echo -e "${SUCCESS} Removed CLI symlink: $LOCAL_BIN/lumen-aura"
    REMOVED_ANY=1
fi

if [ -L "$LOCAL_BIN/aura" ] || [ -f "$LOCAL_BIN/aura" ]; then
    rm -f "$LOCAL_BIN/aura"
    echo -e "${SUCCESS} Removed CLI symlink: $LOCAL_BIN/aura"
    REMOVED_ANY=1
fi

# 3. Remove Application Icons
for icon in \
    "$HICOLOR_DIR/lumen-aura.png" \
    "$HICOLOR_128_DIR/lumen-aura.png" \
    "$PIXMAPS_DIR/lumen-aura.png"; do
    if [ -f "$icon" ]; then
        rm -f "$icon"
        echo -e "${SUCCESS} Removed icon: $icon"
        REMOVED_ANY=1
    fi
done

# 4. Remove Application Payload Directory
if [ -d "$TARGET_DIR" ]; then
    rm -rf "$TARGET_DIR"
    echo -e "${SUCCESS} Removed application directory: $TARGET_DIR"
    REMOVED_ANY=1
fi

# 5. Clean up Desktop & Icon Databases
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APPLICATIONS_DIR" >/dev/null 2>&1 || true
    echo -e "${INFO} Desktop application database updated."
fi

if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f "$LOCAL_SHARE/icons/hicolor" >/dev/null 2>&1 || true
fi

if [ "$REMOVED_ANY" -eq 1 ]; then
    echo -e "\n${SUCCESS} ${BOLD}lumen aura has been completely uninstalled.${NC}\n"
else
    echo -e "\n${WARN} No existing lumen aura installation was found in user space (~/.local).${NC}\n"
fi
