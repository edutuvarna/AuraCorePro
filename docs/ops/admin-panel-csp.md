# admin-panel CSP — operator runbook (Phase 6.13.5)

Live as of 2026-04-25. Server block: `/etc/nginx/sites-enabled/auracore-admin` on origin `165.227.170.3`.

## Current header

```
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: https:; connect-src 'self' https://api.auracore.pro wss://api.auracore.pro; frame-src https://challenges.cloudflare.com; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://api.auracore.pro" always;
```

## Directive rationale

| Directive | Why |
|---|---|
| `default-src 'self'` | Default-deny any origin not explicitly listed below. |
| `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com` | Next.js hydration injects inline scripts (`'unsafe-inline'`); Turnstile uses WebAssembly (`'wasm-unsafe-eval'`); the Turnstile JS bundle is served from `challenges.cloudflare.com`. |
| `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` | Tailwind utilities resolve to inline `<style>` injections; Google Fonts CSS is fetched at runtime. |
| `font-src 'self' https://fonts.gstatic.com` | Outfit + JetBrains Mono font files. |
| `img-src 'self' data: https:` | Avatar / icon `data:` URIs + remote thumbnails. |
| `connect-src 'self' https://api.auracore.pro wss://api.auracore.pro` | XHR + SignalR hub. Turnstile siteverify happens server-side, so CF is NOT in this list. |
| `frame-src https://challenges.cloudflare.com` | Turnstile widget iframe. |
| `object-src 'none'` | Block all plugin embeds. |
| `frame-ancestors 'none'` | Clickjacking defense. |
| `base-uri 'self'` | Block injected `<base>` tag attacks. |
| `form-action 'self' https://api.auracore.pro` | admin-panel forms only POST to its own API. |

`always` ensures the header is sent on non-200 responses too (e.g. 404 pages from Next.js static export).

## Verifying live

```bash
curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy
```

Should return a single `Content-Security-Policy:` response line containing every directive above.

DevTools Console on the panel must be CSP-warning-free. Known-acceptable: zero `Refused to load`/`Refused to execute` lines. Turnstile must render (CF iframe under `frame-src`); SignalR must connect (`wss://api.auracore.pro/hubs/admin` under `connect-src`).

## Updating the header

When admin-panel adds a new external dependency (analytics SDK, font CDN, video host, etc.), the operator MUST update this file and the nginx directive in lockstep:

1. Identify the directive that needs widening.
2. `cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$(date +%Y%m%d%H%M%S)`
3. Edit the directive, save.
4. `nginx -t` → on success, `systemctl reload nginx`.
5. Verify live: `curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy`.
6. Browser smoke (DevTools Console must be CSP-warning-free).
7. Update this `admin-panel-csp.md` file and commit.

## Rollback path

Phase 6.13.5 deploy backup stamp: **`20260425150403`** (file `/etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-20260425150403`).

```bash
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 \
  "cp /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-20260425150403 /etc/nginx/sites-enabled/auracore-admin \
   && nginx -t && systemctl reload nginx"
```

Subsequent CSP edits should append fresh `bak-pre-csp-$STAMP` files; do not overwrite this initial backup until after a verified merge to main + production smoke window.

## Future hardening

- Tighten `'unsafe-inline'` in `script-src` to nonces/hashes once Next.js bundles allow it.
- Tighten `'unsafe-inline'` in `style-src` to per-style nonces if bundle inspection confirms feasibility.
- Trial `Content-Security-Policy-Report-Only` for 1-2 days when introducing future risky directive changes.
