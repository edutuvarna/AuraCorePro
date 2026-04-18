# Phase 5 Debt Sweep — Nginx Origin Cleanup (B3 + B4)

**Date:** 2026-04-17
**Host:** 165.227.170.3 (production origin — `auracore-api`)
**Operator:** root via SSH (AuraCore backend standing authorization — `id_ed25519` key)
**Changes:** (1) archive 8 stale `.bak*` files from `sites-enabled/`; (2) fix double-HSTS by hiding backend's duplicate header.

---

## Pre-state

```
sites-enabled/ had 11 files: 3 live + 8 .bak* backups, all loaded by Nginx.
nginx -T reported 22 "conflicting server name" warnings.
External curl on api.auracore.pro showed 2 Strict-Transport-Security headers
(backend .NET + nginx add_header both firing).
```

## B3 — archive stale .bak files from sites-enabled/

**Files archived** (all moved to `/etc/nginx/sites-available/archive-20260417-debtsweep/`):

| File | Origin context |
|------|----------------|
| `auracore-admin.bak` | Pre-Phase-5.4 backup |
| `auracore-admin.bak.phase5.4` | Phase 5.4 HSTS preload cleanup backup |
| `auracore-api.bak` | Pre-Phase-5.4 backup |
| `auracore-api.bak.bak.phase5.4` | Odd double-suffix; older Phase 5.4 |
| `auracore-api.bak.phase5.4` | Phase 5.4 HSTS preload cleanup backup |
| `auracore-landing.bak` | Pre-Phase-5.4 backup |
| `auracore-landing.bak.phase5.4` | Phase 5.4 HSTS preload cleanup backup |
| `auracore-landing.bak.pre-security` | Pre-security-headers backup |

**Command:**
```bash
mkdir -p /etc/nginx/sites-available/archive-20260417-debtsweep
for f in <list above>; do mv "/etc/nginx/sites-enabled/$f" "/etc/nginx/sites-available/archive-20260417-debtsweep/$f"; done
```

All 8 files preserved for rollback — this is an `mv`, not `rm`.

**Result:** `nginx -T 2>&1 | grep -cE 'conflicting'` → **0** (was 22).

## B4 — fix double-HSTS via `proxy_hide_header`

**Discovery during investigation**: the memory-documented "3 duplicate HSTS directives in auracore-api" was a misdiagnosis. The actual root cause: the backend .NET service at `127.0.0.1:5000` emits `Strict-Transport-Security` on every response, and nginx's `add_header` adds another at the location level. Result: duplicate headers in the final response.

**Verification:**
```bash
curl -sI http://127.0.0.1:5000/    # backend direct
# Strict-Transport-Security: max-age=31536000; includeSubDomains

curl -sI https://api.auracore.pro/ # via nginx
# Strict-Transport-Security: max-age=31536000; includeSubDomains
# Strict-Transport-Security: max-age=31536000; includeSubDomains   <- duplicate
```

**Fix:** add `proxy_hide_header Strict-Transport-Security;` to both `location / { }` and `location /hubs/ { }` blocks in `/etc/nginx/sites-enabled/auracore-api`. This strips the backend's HSTS before nginx appends its own (from the server-level `add_header` for `/hubs/`, or the location-level `add_header` at line 55 for `/`).

**Diff applied:**
```diff
@@ location /hubs/ (~line 22)
     location /hubs/ {
+        proxy_hide_header Strict-Transport-Security;
         proxy_pass http://127.0.0.1:5000;
         ...
     }

@@ location / (~line 62, just before proxy_pass)
         ...
         add_header Cache-Control "no-store" always;
 
+        proxy_hide_header Strict-Transport-Security;
         proxy_pass http://127.0.0.1:5000;
```

**Why NOT the original plan approach** ("remove duplicate `add_header` lines, keep 1 at server scope"): Nginx `add_header` inheritance rule = child context blocks inherit from parent ONLY IF no `add_header` is defined at child. `location / { }` has many CORS-related `add_header` lines, so server-level HSTS would NOT be inherited into `location /`. Removing the location-level HSTS would leave `/` with NO HSTS from nginx — worse than current state.

**Verification (post-reload):**
```
api.auracore.pro       — 1 HSTS (was 2)
api.auracore.pro/hubs/ — 1 HSTS (was 2)
OPTIONS preflight      — 1 HSTS (unchanged, was already clean)
auracore.pro           — 1 HSTS (unchanged)
admin.auracore.pro     — 1 HSTS (unchanged)
```

## Known residual issue (out of debt sweep scope)

`sites-available/auracore-api` and `sites-enabled/auracore-api` are **separate regular files**, not symlinks. This means edits to either won't propagate. The B4 fix was applied only to `sites-enabled/` (which is what Nginx actually loads). `sites-available/auracore-api` still contains the old config without `proxy_hide_header`. If someone ever deletes `sites-enabled/auracore-api` and symlinks `sites-available/auracore-api` in its place, the double-HSTS returns.

**Recommended follow-up (separate task)**: either replace all 3 `sites-enabled/` files with proper symlinks to `sites-available/` counterparts (after syncing content), or document the origin-conf editing contract to always edit `sites-enabled/` directly.

## Rollback

**B3 rollback** (restore any .bak file):
```bash
mv /etc/nginx/sites-available/archive-20260417-debtsweep/<file> /etc/nginx/sites-enabled/<file>
nginx -t && systemctl reload nginx
```

**B4 rollback** (remove proxy_hide_header additions):
```bash
cp /etc/nginx/sites-available/archive-20260417-debtsweep/auracore-api.pre-debtsweep-edit /etc/nginx/sites-enabled/auracore-api
nginx -t && systemctl reload nginx
```

All backups retained at `/etc/nginx/sites-available/archive-20260417-debtsweep/` indefinitely.

## Summary

| Metric | Before | After |
|---|---|---|
| Files in `sites-enabled/` | 11 (3 live + 8 stale) | 3 (live only) |
| `nginx -T` conflicting warnings | 22 | 0 |
| HSTS headers on `api.auracore.pro/` response | 2 (duplicate) | 1 |
| HSTS headers on `api.auracore.pro/hubs/` response | 2 (duplicate) | 1 |
| HSTS present on OPTIONS preflight | yes | yes (unchanged) |
| HSTS coverage on main sites | complete | complete (unchanged) |

External verification confirmed via `curl -sI` against all 5 public endpoints.
