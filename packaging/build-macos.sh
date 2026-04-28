#!/bin/bash
# =============================================================================
# AuraCore Pro — macOS .app bundle builder (UNSIGNED)
# Can run on macOS (native) or Linux/Windows (cross-compile structure only)
# Usage: bash build-macos.sh [version] [arch]
# Example: bash build-macos.sh 1.8.0 arm64
#          bash build-macos.sh 1.8.0 x64
# =============================================================================

set -e

VERSION="${1:-1.8.0}"
ARCH="${2:-arm64}"  # arm64 (Apple Silicon) or x64 (Intel)
RID="osx-${ARCH}"
APP_NAME="AuraCore Pro"
BUNDLE_ID="pro.auracore.app"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT="$REPO_ROOT/src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj"
OUT_DIR="$SCRIPT_DIR/dist"
PUBLISH_DIR="$OUT_DIR/publish-${RID}"
APP_BUNDLE="$OUT_DIR/${APP_NAME}.app"

echo "============================================="
echo "  AuraCore Pro macOS Builder v${VERSION}"
echo "  Architecture: ${ARCH} (${RID})"
echo "============================================="
echo ""

# ── 1. Clean ──
echo "[1/5] Cleaning previous build..."
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

# ── 2. Publish self-contained ──
echo "[2/5] Publishing self-contained for ${RID}..."
dotnet publish "$PROJECT" \
    --nologo \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

echo "    Published $(find "$PUBLISH_DIR" -type f | wc -l) files"

# ── 3. Create .app bundle ──
echo "[3/5] Creating .app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published files to MacOS directory
cp -r "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/AuraCore.Pro"

# Info.plist
cp "$SCRIPT_DIR/Info.plist" "$APP_BUNDLE/Contents/"
# Update version in Info.plist
if command -v sed &> /dev/null; then
    sed -i.bak "s|<string>1.8.0</string>|<string>${VERSION}</string>|g" "$APP_BUNDLE/Contents/Info.plist" 2>/dev/null || true
    rm -f "$APP_BUNDLE/Contents/Info.plist.bak"
fi

# Icon (copy .icns if available, otherwise skip)
if [ -f "$SCRIPT_DIR/AppIcon.icns" ]; then
    cp "$SCRIPT_DIR/AppIcon.icns" "$APP_BUNDLE/Contents/Resources/"
fi

# PkgInfo
echo -n "APPL????" > "$APP_BUNDLE/Contents/PkgInfo"

echo "    Created: $APP_BUNDLE"

# ── 4. Create DMG (macOS only) ──
echo "[4/5] Creating DMG..."
DMG_FILE="$OUT_DIR/AuraCorePro-${VERSION}-macOS-${ARCH}.dmg"

if command -v hdiutil &> /dev/null; then
    # Create a temporary DMG directory
    DMG_STAGING="$OUT_DIR/dmg-staging"
    mkdir -p "$DMG_STAGING"
    cp -r "$APP_BUNDLE" "$DMG_STAGING/"
    
    # Create Applications symlink for drag-to-install
    ln -s /Applications "$DMG_STAGING/Applications"
    
    # Create DMG
    hdiutil create -volname "AuraCore Pro" \
        -srcfolder "$DMG_STAGING" \
        -ov -format UDZO \
        "$DMG_FILE"
    
    rm -rf "$DMG_STAGING"
    echo "    Created: $DMG_FILE"
else
    echo "    [SKIP] hdiutil not available (not on macOS)."
    echo "    .app bundle is ready — zip it for distribution:"
    echo "    cd $OUT_DIR && zip -r AuraCorePro-${VERSION}-macOS-${ARCH}.zip '${APP_NAME}.app'"
    
    # Create zip as fallback
    cd "$OUT_DIR"
    zip -r "AuraCorePro-${VERSION}-macOS-${ARCH}.zip" "${APP_NAME}.app"
    echo "    Created: AuraCorePro-${VERSION}-macOS-${ARCH}.zip"
fi

# ── 5. Create tarball too ──
echo "[5/5] Creating tarball..."
cd "$OUT_DIR"
TARBALL="AuraCorePro-${VERSION}-macOS-${ARCH}.tar.gz"
tar -czf "$TARBALL" "${APP_NAME}.app"
echo "    Created: $TARBALL"

# ── Summary ──
echo ""
echo "============================================="
echo "  Build Complete!"
echo "============================================="
echo ""
echo "  Output directory: $OUT_DIR"
echo ""
ls -lh "$OUT_DIR"/*.{dmg,zip,tar.gz} 2>/dev/null || true
echo ""
echo "  IMPORTANT: This build is UNSIGNED."
echo "  Users need to right-click > Open on first launch,"
echo "  or run: xattr -cr '${APP_NAME}.app'"
echo ""
echo "  For signed builds, enroll in Apple Developer Program"
echo "  (99 USD/year) and use codesign + notarytool."
echo ""
