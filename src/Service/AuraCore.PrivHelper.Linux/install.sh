#!/usr/bin/env bash
# AuraCorePro privilege helper installer.
# Run under pkexec (i.e. as root after user authentication).
# Idempotent: repeated runs are safe — file replacements only happen when
# source/dest differ (byte-level compare).
#
# Usage: pkexec bash install.sh <stage-dir>
#   <stage-dir> must contain:
#     - privhelper                           (daemon binary, executable)
#     - pro.auracore.privhelper.policy       (polkit policy XML)
#     - pro.auracore.privhelper.service      (systemd unit)

set -euo pipefail
umask 022

STAGE="${1:-}"
if [[ -z "$STAGE" || ! -d "$STAGE" ]]; then
    echo "ERROR: stage dir '$STAGE' does not exist or was not provided" >&2
    exit 2
fi

# Canonical install targets (spec-locked; changing these breaks
# PrivHelperInstaller.cs, polkit policy discovery, and the systemd unit).
BINARY_SRC="$STAGE/privhelper"
POLICY_SRC="$STAGE/pro.auracore.privhelper.policy"
SERVICE_SRC="$STAGE/pro.auracore.privhelper.service"

BINARY_DST="/usr/local/lib/auracore/privhelper"
POLICY_DST="/usr/share/polkit-1/actions/pro.auracore.privhelper.policy"
SERVICE_DST="/usr/lib/systemd/system/pro.auracore.privhelper.service"

for f in "$BINARY_SRC" "$POLICY_SRC" "$SERVICE_SRC"; do
    if [[ ! -f "$f" ]]; then
        echo "ERROR: missing source file: $f" >&2
        exit 3
    fi
done

# Idempotent copy: skip if byte-identical with destination.
install_if_changed() {
    local src="$1"
    local dst="$2"
    local mode="$3"

    if [[ -f "$dst" ]] && cmp -s "$src" "$dst"; then
        echo "unchanged: $dst"
        return 0
    fi

    # install(1) handles owner/mode atomically
    install -m "$mode" "$src" "$dst"
    echo "installed: $dst"
}

mkdir -p /usr/local/lib/auracore
install_if_changed "$BINARY_SRC"  "$BINARY_DST"  755
install_if_changed "$POLICY_SRC"  "$POLICY_DST"  644
install_if_changed "$SERVICE_SRC" "$SERVICE_DST" 644

# systemd reload — harmless when nothing actually changed.
systemctl daemon-reload || true

# polkit reload — try the three known paths in decreasing order of
# reliability. Each suffixed with || true so the script does not fail
# on distros where a given mechanism is absent.
if systemctl reload polkit 2>/dev/null; then
    :
elif pkill -HUP polkitd 2>/dev/null; then
    :
elif command -v dbus-send >/dev/null 2>&1; then
    dbus-send --system --dest=org.freedesktop.PolicyKit1 --print-reply         /org/freedesktop/PolicyKit1/Authority         org.freedesktop.PolicyKit1.Authority.ReloadConfiguration         >/dev/null 2>&1 || true
fi

echo "OK: auracore-privhelper installed"
exit 0
