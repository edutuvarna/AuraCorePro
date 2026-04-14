# Admin Panel Production Deploy — Design Spec

**Date:** 2026-04-14
**Status:** Approved
**Scope:** Deploy updated AuraCore.API to production; ship all admin endpoints that currently return 404.

---

## Context

Production API at `https://api.auracore.pro` runs a build dated **2026-04-13 01:57**. The source tree has new admin controllers (revenue, audit log, devices, telemetry, crash reports, IP whitelist) that have never been deployed.

**Endpoint status on production:**

| Endpoint | Production Status | Expected (post-deploy) |
|----------|-------------------|------------------------|
| `/health` | 200 | 200 |
| `/api/admin/dashboard/stats` | 401 | 401 |
| `/api/admin/users` | 401 | 401 |
| `/api/admin/config` | 401 | 401 |
| `/api/admin/revenue/chart-data` | **404** | 401 |
| `/api/admin/audit-log` | **404** | 401 |
| `/api/admin/devices` | **404** | 401 |
| `/api/admin/telemetry` | **404** | 401 |
| `/api/admin/crash-reports` | **404** | 401 |
| `/api/admin/ip-whitelist` | **404** | 401 |

---

## Production Environment

| Item | Value |
|------|-------|
| Server IP | 165.227.170.3 |
| Hostname | auracore-api |
| SSH key | `~/.ssh/id_ed25519` |
| User | root |
| Service unit | `auracore-api.service` |
| Deploy path | `/var/www/auracore-api/` |
| Env file | `/etc/auracore-api.env` |
| Deployment type | Self-contained (linux-x64 binary present) |

---

## Changes

### 1. Version Bump

**File:** `src/Backend/AuraCore.API/Program.cs` (line 161)

**Change:** `version = "1.0.0"` → `version = "1.7.0"` in the non-production health response.

### 2. Publish & Deploy

**Local publish command:**
```
dotnet publish src/Backend/AuraCore.API/AuraCore.API.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o ./publish-linux
```

**Upload via SCP:**
```
scp -i ~/.ssh/id_ed25519 -r ./publish-linux/* \
  root@165.227.170.3:/var/www/auracore-api/
```

**Restart service:**
```
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 \
  "systemctl restart auracore-api && systemctl status auracore-api --no-pager"
```

### 3. Smoke Test

Hit all 9 admin endpoints via `curl`. Every one must return **401** (auth required) — not 404. Health endpoint returns 200.

---

## Explicitly Out of Scope

- **SignalR hub registration** — Not in current codebase. Real feature, needs its own spec + plan. Not part of this deploy.
- **Database migrations** — Auto-migrate runs on startup; no manual intervention needed.
- **Environment variables** — `/etc/auracore-api.env` stays as-is.
- **Nginx config** — Already working (HTTPS, reverse proxy).
- **Admin frontend** (`admin.auracore.pro`) — Separate deployment; not touched here.

---

## Rollback Plan

If the new build breaks production:

1. Keep backup on server: `cp -r /var/www/auracore-api /var/www/auracore-api.bak-20260414` before deploy
2. On failure: `systemctl stop auracore-api && rm -rf /var/www/auracore-api && mv /var/www/auracore-api.bak-20260414 /var/www/auracore-api && systemctl start auracore-api`

---

## Success Criteria

- [x] `/health` returns 200 with `"version":"1.7.0"` in non-production response shape (production response shape stays minimal)
- [x] All 9 admin endpoints return 401 (not 404)
- [x] Service status is `active (running)` after restart
- [x] No errors in `journalctl -u auracore-api` after restart
