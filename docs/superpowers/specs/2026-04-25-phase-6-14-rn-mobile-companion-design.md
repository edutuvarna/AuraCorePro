# Phase 6.14 — React Native Mobile Companion App — Design

**Date:** 2026-04-25
**Author:** Brainstormed in collaborative dialogue (Phase 6.13 closeout session)
**Status:** Draft, ready for writing-plans skill in same session
**Successor to:** [Phase 6.13 UX Polish + FE Defense (merged at `c7943ea`)](../../../memory/project_phase_6_item_13_ux_polish_complete.md)

## Goal

Ship a **mobile-only Android companion app** built with React Native (Expo) that lets superadmins receive permission-request alerts and approve/deny them on the go. Web admin-panel at `admin.auracore.pro` stays the primary, full-feature platform — mobile is distilled "alerts + quick action" surface.

## Non-goals

- **Not replacing the Next.js admin-panel.** Web stays primary, full-feature. Mobile is companion only.
- **No iOS in 6.14.** Apple Developer hesabı yok; iOS port → 6.16+ carry-forward (RN'in cross-platform yapısı sayesinde port maliyeti düşük olacak).
- **No Play Store distribution in 6.14.** Sideload `.apk`. Play Store Internal Testing → 6.15+ (Google Play Developer hesabı $25 alındığında).
- **No feature parity goal ever.** Mobile is forever a distilled subset. Bulk operations, audit log review, security policy editing — desktop-only by design.
- **No incident-response feature pack** (Users list, ban, force-logout) → 6.15.
- **No TOTP backup codes / RateLimiter hot-reload / audit retention dashboard** in 6.14 — all carry-forward → 6.15.
- **No SignalR on RN.** FCM-only realtime channel.

## Scope (4 screens, MVP)

| # | Screen | Behavior |
|---|---|---|
| 1 | Login | Email + password + 2FA TOTP + biometric enrollment prompt; FCM token registration on success |
| 2 | Dashboard | 4 KPI cards (active users, subscriptions, MRR, recent payments) + activity feed; pull-to-refresh |
| 3 | Permission Requests | Superadmin-only list with approve/deny (deny opens modal for reviewNote); push-driven cache invalidation |
| 4 | Settings | Profile email + sign out |

Backend additions (small, focused):
- `fcm_device_tokens` table + `POST/DELETE /api/admin/me/fcm-token` endpoints
- `IFcmService` (FCM HTTP v1 API) injected into `PermissionRequestController` create handler
- `AuthController.RedeemInvitation` returns `requiresTwoFactorSetup` flag (cleans up Phase 6.13 sessionStorage shim's reason for being — mobile RN parses it directly; web shim itself stays as-is until a separate refactor)

## Locked design decisions

- **D1: Pure platform shift scope.** No carry-forward features. RN platform validation only. Bulk role change, force-logout-all, RateLimiter hot-reload, TOTP backup codes, audit retention dashboard → all 6.15+.
- **D2: Mobile-only companion.** Web admin-panel at `admin.auracore.pro` stays primary. Two codebases by design — each platform-optimal.
- **D3: Android only.** iOS deferred — Apple Developer account ($99/yr) not available yet.
- **D4: Feature MVP = Auth + Dashboard + Permission Requests + Push.** "On-the-go alert + approve" is the value prop. Users list / ban / force-logout (option C from Q4) → 6.15.
- **D5: Expo (managed) + EAS Build + NativeWind v4 + Expo Router + TanStack Query.** Standard 2026 RN stack. NativeWind keeps the web admin's Tailwind mental model.
- **D6: Sideload `.apk` distribution.** Play Store Internal Testing future-work. EAS Build supports both — switch is a profile change.
- **D7: Biometric unlock + secure JWT cache.** `expo-secure-store` (Android Keystore) with `requireAuthentication: true`. 3 biometric failures → password fallback. 30-day inactivity → full re-auth.
- **D8: FCM-only realtime, hybrid presentation (Option C from Q8).** Single FCM channel for foreground + background. Foreground = `setNotificationHandler` suppresses system banner, custom glass-card in-app banner via `addNotificationReceivedListener` rendered. Background = default Android heads-up. Backend trigger emits SignalR (web) AND FCM (mobile) in parallel from the same handler.
- **D9: Backend `requiresTwoFactorSetup` flag emit from `redeem-invitation`.** Mobile RN reads it directly. Web admin sessionStorage shim from Phase 6.13 stays as-is (working) — proper web refactor → 6.16.
- **D10: Web-only invitation deep links.** No Android intent filter / App Links. New admins onboard on web (one-time event), then move to mobile for ongoing work. FCM tap-to-route is `expo-notifications` built-in (not Android deep linking).
- **D11: Session inactivity 30 days.** Matches refresh-token lifetime. Cold start checks `lastActiveAt` in secure-store; >30d → flush.
- **D12: Package id `com.auracore.admin`, display "AuraCore Admin".**

## Architecture

### Repo layout

```
AuraCorePro/
├── admin-panel/              # Next.js web admin (UNCHANGED in 6.14)
├── mobile/                   # NEW — Expo RN app
│   ├── app/                  # Expo Router file-system routes
│   │   ├── _layout.tsx       # Root: Providers (TanStack Query, Notifications, AuthGuard)
│   │   ├── (auth)/
│   │   │   ├── _layout.tsx   # Unauth-only stack
│   │   │   └── login.tsx     # Email + password + 2FA + biometric enrollment
│   │   └── (app)/
│   │       ├── _layout.tsx   # Authed shell (bottom tabs)
│   │       ├── index.tsx     # Dashboard
│   │       ├── permissions.tsx
│   │       └── settings.tsx
│   ├── src/
│   │   ├── lib/
│   │   │   ├── api.ts            # fetch wrapper + JWT injection (mirrors admin-panel pattern)
│   │   │   ├── secureStore.ts    # JWT cache + lastActiveAt + biometric-gated read
│   │   │   ├── notifications.ts  # FCM register, handler config, route on tap
│   │   │   ├── auth.ts           # login / logout / biometric unlock state machine
│   │   │   └── queryClient.ts    # TanStack Query default config
│   │   ├── components/
│   │   │   ├── KpiCard.tsx
│   │   │   ├── InAppNotificationBanner.tsx  # custom foreground banner (Option C)
│   │   │   ├── PermissionRequestRow.tsx
│   │   │   └── DenyModal.tsx
│   │   └── hooks/
│   │       └── usePermissionRequests.ts     # TanStack Query wrapper
│   ├── __tests__/                # jest + react-native-testing-library
│   ├── assets/
│   │   ├── icon.png              # 1024x1024
│   │   ├── adaptive-icon.png     # Android adaptive
│   │   └── splash.png
│   ├── app.json                  # Expo config (package id, scheme, FCM google-services)
│   ├── eas.json                  # EAS Build profiles (preview, production)
│   ├── metro.config.js           # NativeWind preset
│   ├── babel.config.js           # NativeWind babel plugin
│   ├── tailwind.config.js        # cyan/purple/surface palette mirrored from admin-panel
│   ├── tsconfig.json
│   └── package.json
│
├── src/Backend/                  # Backend (small additions in 6.14)
└── docs/superpowers/specs/2026-04-25-phase-6-14-rn-mobile-companion-design.md  # this spec
```

### Tech stack

- **Expo SDK 51+** (RN 0.74+ with New Architecture default)
- **TypeScript** strict (mirrors admin-panel)
- **NativeWind v4** — Tailwind className syntax in RN
- **Expo Router 3** — file-system routing
- **TanStack Query v5** — server-state cache + retry on reconnect
- **expo-notifications** — FCM wrapper
- **expo-local-authentication** — biometric (fingerprint/face)
- **expo-secure-store** — Android Keystore JWT
- **Jest + react-native-testing-library** + `jest-expo` preset

### Tab nav

Bottom tabs (native): Dashboard / Permissions (superadmin only) / Settings. Admin-role users see Dashboard + Settings only. The `Permissions` tab visibility is `useRole() === 'superadmin'`.

### NativeWind palette (mirrored from admin-panel `tailwind.config.js`)

```js
colors: {
  surface: { 700: '#15151a', 800: '#0d0d12', 900: '#08080c', 950: '#060911' },
  accent: { DEFAULT: '#22d3ee', light: '#22D3EE', dark: '#0891B2', glow: 'rgba(6,182,212,0.15)', secondary: '#a78bfa' },
  aura: { cyan: '#22d3ee', purple: '#a78bfa', green: '#34d399', red: '#f87171', amber: '#fbbf24' }
}
```

`bg-surface-900`, `bg-white/[0.03] backdrop-blur`, `border-white/[0.08]` etc. work the same as admin-panel — same `.glass-card` aesthetic, just rendered via NativeWind on native instead of CSS on web.

## Auth flow

```
[Cold start] → AuthProvider mount
   │
   ├─→ secureStore.getJwt() AND secureStore.getLastActiveAt()
   │       ├─→ JWT exists AND now() − lastActiveAt < 30 days
   │       │       └─→ LocalAuthentication.authenticateAsync({ promptMessage: 'Unlock AuraCore' })
   │       │             ├─→ Success → JWT decrypt → /api/admin/dashboard/stats probe
   │       │             │       ├─→ 200 → setAuth({ jwt, role }) → router.replace('/(app)')
   │       │             │       └─→ 401 → secureStore.flush() → router.replace('/(auth)/login')
   │       │             └─→ 3x failed/cancel → router.replace('/(auth)/login') (password fallback)
   │       │
   │       └─→ JWT missing OR >30 days → secureStore.flush() → router.replace('/(auth)/login')
   │
   └─→ /(auth)/login → email + password → if requires2fa → TOTP step → JWT + refresh issued
           → secureStore.setJwt(token, { requireAuthentication: true })
           → secureStore.setLastActiveAt(now())
           → if (firstLogin) → biometric enrollment prompt
           → register FCM token via Notifications.getExpoPushTokenAsync() + POST /api/admin/me/fcm-token
           → router.replace('/(app)')
```

**Logout:**
1. `DELETE /api/admin/me/fcm-token` (auth header still valid)
2. `secureStore.flush()` (token + lastActiveAt cleared)
3. `queryClient.clear()`
4. `router.replace('/(auth)/login')`

## Push notification flow (Option C — hybrid single-channel)

### Token registration (post-login)

```ts
// mobile/src/lib/notifications.ts
import * as Notifications from 'expo-notifications';

export async function registerForPush() {
  const settings = await Notifications.getPermissionsAsync();
  if (settings.status !== 'granted') {
    const ask = await Notifications.requestPermissionsAsync();
    if (ask.status !== 'granted') return null;
  }
  const token = await Notifications.getExpoPushTokenAsync({ projectId: process.env.EXPO_PROJECT_ID });
  await api.post('/admin/me/fcm-token', { token: token.data, platform: 'android' });
  return token.data;
}
```

### Foreground handler (custom banner)

```ts
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: false,   // suppress system banner
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

// in _layout.tsx:
useEffect(() => {
  const sub = Notifications.addNotificationReceivedListener(notification => {
    const data = notification.request.content.data;
    showInAppBanner(data);  // custom <InAppNotificationBanner> render
    if (data.type === 'permission-request') {
      queryClient.invalidateQueries({ queryKey: ['permissionRequests'] });
    }
  });
  return () => sub.remove();
}, []);
```

### Background (default — system heads-up)

`setNotificationHandler` foreground-only; backgrounded app uses Android default heads-up notification. expo-notifications handles this — no extra config.

### Tap routing

```ts
useEffect(() => {
  const sub = Notifications.addNotificationResponseReceivedListener(response => {
    const data = response.notification.request.content.data;
    if (data.type === 'permission-request') {
      router.push(`/permissions?focus=${data.requestId}`);
    }
  });
  return () => sub.remove();
}, []);
```

### Backend trigger (PermissionRequestController create handler)

```csharp
// after existing SignalR emit
await _hub.Clients.Group("superadmin").SendAsync("PermissionRequested", payload, ct);

// NEW — FCM push for mobile
var superadminTokens = await _db.FcmDeviceTokens
    .Where(t => _db.Users.Where(u => u.Role == "superadmin").Select(u => u.Id).Contains(t.UserId))
    .Select(t => t.Token)
    .ToListAsync(ct);
foreach (var token in superadminTokens) {
    await _fcm.SendAsync(token, new FcmPayload {
        Title = "Permission request",
        Body = $"{adminEmail} requests {permissionKey}",
        Data = new { type = "permission-request", requestId = req.Id.ToString() }
    }, ct);
}
```

## Backend changes

### New entity + migration

```csharp
public sealed class FcmDeviceToken {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "android";  // ios in 6.16
    public string? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
```

EF migration `AddFcmDeviceTokens`:
- Table `fcm_device_tokens`
- PK on `Id` (uuid)
- Index on `UserId` (FK to `users`)
- Unique index on `(UserId, Token)` (dedup)

### New endpoints (extend existing AuthController OR new MeController)

```csharp
[Authorize]
[HttpPost("/api/admin/me/fcm-token")]
public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenDto dto, CancellationToken ct) {
    var userId = User.GetUserId() ?? throw new UnauthorizedAccessException();
    var existing = await _db.FcmDeviceTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.Token == dto.Token, ct);
    if (existing != null) {
        existing.LastSeenAt = DateTimeOffset.UtcNow;
    } else {
        _db.FcmDeviceTokens.Add(new FcmDeviceToken {
            UserId = userId, Token = dto.Token, Platform = dto.Platform,
            CreatedAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow,
        });
    }
    await _db.SaveChangesAsync(ct);
    return Ok();
}

[Authorize]
[HttpDelete("/api/admin/me/fcm-token")]
public async Task<IActionResult> UnregisterFcmToken([FromQuery] string token, CancellationToken ct) {
    var userId = User.GetUserId() ?? throw new UnauthorizedAccessException();
    var row = await _db.FcmDeviceTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, ct);
    if (row != null) {
        _db.FcmDeviceTokens.Remove(row);
        await _db.SaveChangesAsync(ct);
    }
    return NoContent();
}
```

### IFcmService

```csharp
public interface IFcmService {
    Task SendAsync(string deviceToken, FcmPayload payload, CancellationToken ct);
}

public sealed record FcmPayload(string Title, string Body, object Data);

public sealed class FcmService : IFcmService {
    // FCM HTTP v1 API: https://fcm.googleapis.com/v1/projects/{project_id}/messages:send
    // Auth: service-account JWT (Bearer)
    // Service account JSON loaded from FCM_SERVICE_ACCOUNT_JSON env var
}
```

DI: `services.AddSingleton<IFcmService, FcmService>();` + `HttpClient` injection.

### RedeemInvitation flag emit

```csharp
return Ok(new {
    accessToken = access,
    refreshToken = refresh,
    user = new { user.Id, user.Email, user.Role },
    requiresTwoFactorSetup = string.IsNullOrEmpty(user.TotpSecret)  // NEW for mobile parsing
});
```

Web admin's existing sessionStorage shim from Phase 6.13 stays in place — it works, no rush. Mobile RN reads `requiresTwoFactorSetup` from this response directly.

## Security model

- **JWT storage:** `expo-secure-store` `setItemAsync(key, value, { requireAuthentication: true })` — Android Keystore-backed, biometric required for every read.
- **Biometric:** `expo-local-authentication` `authenticateAsync({ promptMessage, fallbackLabel: 'Use password', disableDeviceFallback: false })`. 3 failures → password fallback.
- **Token rotation:** Same as web — 15-min access token, 30-day refresh token.
- **Logout:** Server refresh-token revoke + device secure-store flush + FCM token unregister.
- **Force-logout (6.15+):** Server adds `revoked_tokens` jti → mobile gets 401 on next API → flush + login redirect.
- **FCM token rotation:** Backend `(UserId, Token)` unique constraint dedupes; backend `LastSeenAt` updates on each register call. Stale tokens (FCM rejects with `UNREGISTERED`) cleaned up lazily.

## Testing

### Unit / integration (jest + react-native-testing-library)

| File | Cases |
|---|---|
| `mobile/__tests__/lib/secureStore.test.ts` | roundtrip + 30d inactivity flush |
| `mobile/__tests__/lib/notifications.test.ts` | handler config, foreground banner dispatch, tap routing |
| `mobile/__tests__/views/login.test.tsx` | email/password, 2FA branch, biometric prompt |
| `mobile/__tests__/views/dashboard.test.tsx` | KPI render, refresh on focus |
| `mobile/__tests__/views/permissions.test.tsx` | list render, approve mutation, deny modal |

### Backend (existing test project, AuraCore.Tests.API)

| File | Cases |
|---|---|
| `tests/.../Phase614/FcmDeviceTokenTests.cs` | register dedup, unregister, 401 unauth (4-5 cases) |
| `tests/.../Phase614/RedeemInvitationFlagTests.cs` | TotpSecret null → flag true; not null → flag false (2 cases) |
| `tests/.../Phase614/PermissionRequestPushTriggerTests.cs` | mocked IFcmService called with correct payload (1-2 cases) |

### Manual E2E

- Build `.apk` via `eas build --profile preview --platform android`
- Install on Android device via downloaded APK
- Full flow: cold start → password login → 2FA → biometric enrollment → dashboard renders → backend creates a permission request → push arrives → app shows in-app banner (foreground) or system heads-up (backgrounded) → tap → permissions tab focuses on the request → approve → row disappears → backend audit log shows action

### Test budgets

- Backend: 208 → ~213-215 (+5-7 from FCM endpoint + RedeemInvitation flag + push trigger tests)
- Mobile: 0 → ~15-20 (new project)

## Build / deploy

### EAS Build profiles (`mobile/eas.json`)

```json
{
  "build": {
    "preview": {
      "android": { "buildType": "apk" },
      "distribution": "internal"
    },
    "production": {
      "android": { "buildType": "apk" },
      "distribution": "internal",
      "channel": "production"
    }
  }
}
```

`distribution: "internal"` here means EAS-internal (not Google Play Internal Testing — that's `distribution: "store"`). Generates a downloadable APK URL.

### Sideload distribution

After `eas build --profile preview` completes:
- EAS dashboard provides direct APK download URL (auth-gated to project members)
- Send URL to admin team via Slack / secure channel
- Admin enables "Install from unknown sources" once, installs APK
- For updates: rebuild + redistribute URL (manual reinstall)
- **OTA via `eas update`** (Phase 6.15+): JS-only changes push to running app without reinstall

### Backend deploy

Backend changes (FCM endpoint + redeem-invitation flag + IFcmService) follow the standard pattern:
- `dotnet publish -c Release -o /tmp/auracore-api-publish-6.14`
- scp + chown + systemctl restart auracore-api
- Smoke: `curl -sS https://api.auracore.pro/api/admin/me/fcm-token` → 401 (auth required) confirms route registered

### Database migration

EF migration `AddFcmDeviceTokens` runs on backend startup if `__EFMigrationsHistory` is in use. **Pre-deploy check:** confirm production `__EFMigrationsHistory` baseline matches local before applying. Phase 6.8 seeded baseline; subsequent migrations chain from there.

## Operational steps (for execution)

### Local-only steps (safe to do AFK)

1. Create `mobile/` Expo project
2. Configure NativeWind v4 + Tailwind palette
3. Set up Expo Router skeleton
4. Implement secureStore + auth state machine
5. Build Login + Dashboard + Permissions + Settings screens
6. Wire FCM via expo-notifications
7. Backend: add FcmDeviceToken entity + migration + endpoints + IFcmService
8. Backend: add `requiresTwoFactorSetup` to RedeemInvitation
9. Write tests (mobile + backend)
10. EAS Build → APK locally testable
11. Commit everything to a feature branch `phase-6-14-rn-mobile`

### Live origin steps (requires user approval before each)

12. Apply EF migration on production DB (low risk — additive table, no data churn)
13. Deploy backend (FCM endpoint live, push trigger active)
14. Set FCM_SERVICE_ACCOUNT_JSON env var on origin (`/etc/auracore-api.env`, perms 600 www-data)
15. Manual E2E smoke on physical Android device

### Branch / merge

- Feature branch: `phase-6-14-rn-mobile`
- Pattern: same as 6.13 — `--no-ff` merge to main + ceremonial close

## Risks

- **EAS Free tier limit (30 builds/month).** MVP iteration may hit this if many incremental builds. Mitigation: do most work via `expo start --dev-client` (local dev), use EAS only for installable APK.
- **FCM token-handling on logout:** if logout fails mid-flow (network drop after JWT cache flush but before FCM unregister API call), backend has stale FCM rows. Mitigation: stale token cleanup on first FCM send-failure (`UNREGISTERED` response), backend deletes the row.
- **Backgrounded SignalR vs FCM divergence:** web admin uses SignalR (in-browser), mobile uses FCM. Both transport the same business event but via different paths. If backend handler emits SignalR but FCM call fails (network, FCM outage), web users see the event live but mobile users miss the push. Mitigation: log FCM failures to audit_log; manual replay possible from there.
- **Biometric reset by user:** if user rotates fingerprint/face on Android, secure-store JWT becomes inaccessible (Keystore key tied to biometric enrollment). Result: silent failure on `getItemAsync` → fall back to password. UX-acceptable.
- **First-time RN dev for this project:** team has zero RN history (web-only until now). Phase 6.14's "platform validation" goal explicitly accepts that first-time learning-curve cost.

## Future work (carry-forward → 6.15+)

- iOS port (Apple Developer account + Mac required)
- Play Store Internal Testing migration ($25 Google fee)
- Incident-response feature pack (Users list, ban, force-logout) — Q4 option C
- TOTP backup codes (also relevant on mobile — recovery if user loses phone)
- Bulk role change, audit retention dashboard, RateLimiter hot-reload (carry-forward from 6.13)
- Web admin RedeemInvitationPage proper refactor (drop Phase 6.13 sessionStorage shim)
- OTA updates via `eas update` (Phase 6.15+)
- Deep linking (Android intent filter for `https://admin.auracore.pro/#/invite` and possibly `auracore://` scheme)
- Push notification quick-action buttons (Approve / Deny inline) — backend FCM payload + intent handler

## Self-review notes

- **Placeholders:** none.
- **Internal consistency:** D2 (mobile-only companion) consistent with goal + non-goals + scope. D8 (FCM-only Option C) consistent with section 4 push flow. D9 (backend flag emit but web shim stays) consistent with backend changes section.
- **Scope check:** Single-implementation-plan scope. Mobile project + small backend additions are coupled (push end-to-end), one plan. Decomposition into separate sub-projects unnecessary.
- **Ambiguity check:** "Manual E2E" in testing — explicit list of steps under section "Manual E2E" resolves ambiguity. EAS Build profile naming (preview vs production) is project convention — preview = development APK, production = EAS Update channel.

## Continuity note

User pre-approved all design sections during the dialogue + delegated spec self-review and user-review-of-spec gates ("Plan'ı yazdıktan hemen sonra subagent driven olarak execution geç"). Standing prefs from memory: subagent-driven, supervisor mode, auto-deploy ONLY for critical security bugs (this phase is polish/feature — pause for deploy approval). User AFK during execution. Resume command:

```
Read C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro\docs\superpowers\specs\2026-04-25-phase-6-14-rn-mobile-companion-design.md.
Branch off main HEAD `34afcea` (Phase 6.13 close).
Local-only execution: scaffold Expo project, build screens, write tests.
Live origin deploy steps (backend FCM service + EF migration + env var) PAUSE FOR USER APPROVAL.
```
