# HSTS preload directive cleanup — 2026-04-17

## Context

Phase 5.4 item 6: origin Nginx was sending
`Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`
on all three `auracore.pro` subdomains (root, `admin.`, `api.`). The
`preload` token is inert unless the domain is submitted to
`hstspreload.org`, but keeping it is risky — accidental submission
would lock the domain into the Chrome preload list for months (6+ on
modern browsers), making future HSTS rollback painful.

This change drops the `preload` token while keeping `max-age=31536000`
(1 year) and `includeSubDomains`.

## Target correction vs. spec

The Phase 5.4 spec expected the origin at `164.92.228.183`. Discovery
found that IP belongs to the **nanoclaw Kali droplet** (personal AI
infra — memory: `project_nanoclaw_kali_droplet.md`). The actual
`auracore.pro` origin Nginx runs at **`165.227.170.3`** (resolved via
`dig +short auracore.pro`).

Spec / plan / future deploy docs should reference `165.227.170.3` as
the origin. Update the spec reference in a future Phase 5 cleanup pass
if needed.

## Session summary

- **Host:** `165.227.170.3` (auracore.pro origin)
- **Timestamp:** 2026-04-17 08:15 UTC
- **Auth:** passwordless SSH (`root@165.227.170.3` key-based)
- **Operator:** Claude Code session under user's direct supervision
- **Branch at time of ops:** `phase-5-polish` HEAD `bd47650` (Task 12)

## Before

```
=== auracore.pro ===
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
=== admin.auracore.pro ===
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

## Change

Four Nginx site configs carried the directive (6 total lines across them):

| File | Lines |
|---|---|
| `/etc/nginx/sites-enabled/auracore-landing` | 5 |
| `/etc/nginx/sites-enabled/auracore-api` | 10, 46, 55 |
| `/etc/nginx/sites-enabled/auracore-admin` | 5 |
| `/etc/nginx/sites-enabled/auracore-api.bak` | 10 |

Commands executed (as `root@165.227.170.3`):

```bash
cd /etc/nginx/sites-enabled
for f in auracore-landing auracore-api auracore-admin auracore-api.bak; do
  cp -p $f $f.bak.phase5.4
  sed -i 's/; preload//g' $f
done

nginx -t
nginx -s reload
```

Validation output:

```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
reload OK
```

Pre-existing `conflicting server name` warnings (from the stale
`auracore-api.bak` duplicating `api.auracore.pro` / `auracore.pro`
server_names in `sites-enabled/`) surfaced during `nginx -t` but are
NOT new — they predate this change. Nginx ignores the duplicate
definitions and picks the first-loaded. A separate future cleanup
should either move `.bak` out of `sites-enabled/` or delete it.

## After

```
=== auracore.pro ===
Strict-Transport-Security: max-age=31536000; includeSubDomains

=== admin.auracore.pro ===
Strict-Transport-Security: max-age=31536000; includeSubDomains

=== api.auracore.pro ===
Strict-Transport-Security: max-age=31536000; includeSubDomains
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

(The `api.auracore.pro` double-header is pre-existing — three
`add_header` locations in the API conf all match the same request
path. They now both lack `preload`; non-preload duplicates are
harmless. Consolidating to a single `add_header` is a separate
tidy-up.)

Verified from outside via:
```bash
curl -sI https://auracore.pro | grep -i strict
curl -sI https://admin.auracore.pro | grep -i strict
curl -sI https://api.auracore.pro | grep -i strict
```

## Rollback

Backup copies preserved at:
- `/etc/nginx/sites-enabled/auracore-landing.bak.phase5.4`
- `/etc/nginx/sites-enabled/auracore-api.bak.phase5.4`
- `/etc/nginx/sites-enabled/auracore-admin.bak.phase5.4`
- `/etc/nginx/sites-enabled/auracore-api.bak.bak.phase5.4`

To roll back:
```bash
ssh root@165.227.170.3
cd /etc/nginx/sites-enabled
for f in auracore-landing auracore-api auracore-admin auracore-api.bak; do
  cp -p $f.bak.phase5.4 $f
done
nginx -t && nginx -s reload
```

## Security impact

- Domains are NOT submitted to `hstspreload.org`; removing the token
  has no external effect beyond future-submission gating.
- Clients that cached the old `preload` directive will continue to
  treat it as preloaded until `max-age` (1 year) expires — but since
  the domain isn't in the Chrome preload list, this is a no-op in
  practice.
- `includeSubDomains` preserved — all `*.auracore.pro` subdomains
  remain HSTS-protected at the header level.
- `max-age=31536000` preserved — 1-year HSTS retention unchanged.

No new attack surface introduced.
