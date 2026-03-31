#!/bin/bash
# =============================================================================
# AuraCore Pro — Linux .deb + tarball builder
# Run on a Linux machine with .NET 8 SDK installed
# Usage: bash build-linux.sh [version]
# Example: bash build-linux.sh 1.7.0
# =============================================================================

set -e

VERSION="${1:-1.7.0}"
APP_NAME="auracorepro"
DISPLAY_NAME="AuraCore Pro"
ARCH="amd64"
MAINTAINER="Deniz <admin@auracore.pro>"
DESCRIPTION="Cross-platform system optimization and customization tool"
HOMEPAGE="https://auracore.pro"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT="$REPO_ROOT/src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj"
OUT_DIR="$SCRIPT_DIR/dist"
PUBLISH_DIR="$OUT_DIR/publish-linux-x64"

echo "============================================="
echo "  AuraCore Pro Linux Builder v${VERSION}"
echo "============================================="
echo ""

# ── 1. Clean previous build ──
echo "[1/6] Cleaning previous build..."
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

# ── 2. Publish self-contained ──
echo "[2/6] Publishing self-contained for linux-x64..."
dotnet publish "$PROJECT" \
    --nologo \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

echo "    Published $(find "$PUBLISH_DIR" -type f | wc -l) files"

# ── 3. Create tarball (.tar.gz) ──
echo "[3/6] Creating tarball..."
TARBALL="$OUT_DIR/${APP_NAME}-${VERSION}-linux-x64.tar.gz"
cd "$OUT_DIR"
mv publish-linux-x64 "$APP_NAME"
# Add launcher script
cat > "$APP_NAME/run.sh" << 'LAUNCHER'
#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$DIR/AuraCore.UI.Avalonia" "$@"
LAUNCHER
chmod +x "$APP_NAME/run.sh"
chmod +x "$APP_NAME/AuraCore.UI.Avalonia"
tar -czf "$TARBALL" "$APP_NAME"
echo "    Created: $TARBALL"

# ── 4. Build .deb package ──
echo "[4/6] Building .deb package..."
DEB_DIR="$OUT_DIR/deb-staging"
DEB_FILE="$OUT_DIR/${APP_NAME}_${VERSION}_${ARCH}.deb"

mkdir -p "$DEB_DIR/DEBIAN"
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME"
mkdir -p "$DEB_DIR/usr/bin"
mkdir -p "$DEB_DIR/usr/share/applications"
mkdir -p "$DEB_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$DEB_DIR/usr/share/pixmaps"

# Copy published files
cp -r "$OUT_DIR/$APP_NAME/"* "$DEB_DIR/usr/lib/$APP_NAME/"
chmod +x "$DEB_DIR/usr/lib/$APP_NAME/AuraCore.UI.Avalonia"

# Create launcher script
cat > "$DEB_DIR/usr/bin/$APP_NAME" << BINSCRIPT
#!/bin/bash
exec /usr/lib/$APP_NAME/AuraCore.UI.Avalonia "\$@"
BINSCRIPT
chmod +x "$DEB_DIR/usr/bin/$APP_NAME"

# Desktop entry
cp "$SCRIPT_DIR/auracorepro.desktop" "$DEB_DIR/usr/share/applications/"

# Icon (use a placeholder if no icon exists)
if [ -f "$SCRIPT_DIR/auracorepro.png" ]; then
    cp "$SCRIPT_DIR/auracorepro.png" "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/"
    cp "$SCRIPT_DIR/auracorepro.png" "$DEB_DIR/usr/share/pixmaps/"
fi

# Calculate installed size
INSTALLED_SIZE=$(du -sk "$DEB_DIR" | cut -f1)

# Control file
cat > "$DEB_DIR/DEBIAN/control" << CONTROL
Package: $APP_NAME
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Installed-Size: $INSTALLED_SIZE
Depends: libx11-6, libice6, libsm6, libfontconfig1
Maintainer: $MAINTAINER
Homepage: $HOMEPAGE
Description: $DESCRIPTION
 AuraCore Pro is a cross-platform system optimization tool.
 Features include junk cleaning, RAM optimization, process monitoring,
 system health analysis, and platform-specific tools for Windows,
 Linux, and macOS.
CONTROL

# Post-install script
cat > "$DEB_DIR/DEBIAN/postinst" << 'POSTINST'
#!/bin/bash
update-desktop-database /usr/share/applications/ 2>/dev/null || true
gtk-update-icon-cache /usr/share/icons/hicolor/ 2>/dev/null || true
POSTINST
chmod 755 "$DEB_DIR/DEBIAN/postinst"

# Build .deb
dpkg-deb --build "$DEB_DIR" "$DEB_FILE"
echo "    Created: $DEB_FILE"

# ── 5. Build AppImage (if appimagetool available) ──
echo "[5/6] Building AppImage..."
APPIMAGE_DIR="$OUT_DIR/AppImage-staging"
APPIMAGE_FILE="$OUT_DIR/AuraCorePro-${VERSION}-x86_64.AppImage"

mkdir -p "$APPIMAGE_DIR/usr/bin"
mkdir -p "$APPIMAGE_DIR/usr/lib/$APP_NAME"
mkdir -p "$APPIMAGE_DIR/usr/share/applications"
mkdir -p "$APPIMAGE_DIR/usr/share/icons/hicolor/256x256/apps"

cp -r "$OUT_DIR/$APP_NAME/"* "$APPIMAGE_DIR/usr/lib/$APP_NAME/"
chmod +x "$APPIMAGE_DIR/usr/lib/$APP_NAME/AuraCore.UI.Avalonia"

# AppRun
cat > "$APPIMAGE_DIR/AppRun" << 'APPRUN'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
exec "$HERE/usr/lib/auracorepro/AuraCore.UI.Avalonia" "$@"
APPRUN
chmod +x "$APPIMAGE_DIR/AppRun"

# Desktop + icon at root level (AppImage requirement)
cp "$SCRIPT_DIR/auracorepro.desktop" "$APPIMAGE_DIR/"
cp "$SCRIPT_DIR/auracorepro.desktop" "$APPIMAGE_DIR/usr/share/applications/"
if [ -f "$SCRIPT_DIR/auracorepro.png" ]; then
    cp "$SCRIPT_DIR/auracorepro.png" "$APPIMAGE_DIR/"
    cp "$SCRIPT_DIR/auracorepro.png" "$APPIMAGE_DIR/usr/share/icons/hicolor/256x256/apps/"
else
    # Create a minimal 1x1 PNG placeholder so AppImage doesn't fail
    echo -ne '\x89PNG\r\n\x1a\n' > "$APPIMAGE_DIR/auracorepro.png"
fi

# Try to build AppImage
if command -v appimagetool &> /dev/null; then
    ARCH=x86_64 appimagetool "$APPIMAGE_DIR" "$APPIMAGE_FILE"
    echo "    Created: $APPIMAGE_FILE"
else
    echo "    [SKIP] appimagetool not found. Install it with:"
    echo "           wget https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    echo "           chmod +x appimagetool-x86_64.AppImage && sudo mv appimagetool-x86_64.AppImage /usr/local/bin/appimagetool"
    echo "    Then re-run this script."
fi

# ── 6. Summary ──
echo ""
echo "============================================="
echo "  Build Complete!"
echo "============================================="
echo ""
echo "  Output directory: $OUT_DIR"
echo ""
ls -lh "$OUT_DIR"/*.{tar.gz,deb,AppImage} 2>/dev/null || true
echo ""
echo "  Install .deb:     sudo dpkg -i $DEB_FILE"
echo "  Extract tarball:  tar -xzf $TARBALL"
echo "  Run AppImage:     chmod +x $APPIMAGE_FILE && ./$APPIMAGE_FILE"
echo ""
