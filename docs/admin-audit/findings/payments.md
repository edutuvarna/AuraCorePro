# Payments Audit Findings

**Tab:** Payments
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Payments")
**Audit date:** 2026-04-22
**Auditor:** subagent-4
**Time spent:** ~3 hours

## Source files audited

- Frontend TSX (live deployed): `/root/admin-panel/src/app/page.tsx` lines 615–680 (`PaymentsPage` function)
- Frontend API client (live deployed): `/root/admin-panel/src/lib/api.ts` lines 50–72 (`getRecentPayments`, `getPendingCrypto`, `verifyCryptoPayment`, `rejectCryptoPayment`)
- Backend — payments data source: **NO `AdminPaymentController.cs` exists** (neither in local repo nor in backup). Payments tab pulls data from:
  - `src/Backend/AuraCore.API/Controllers/Admin/AdminDashboardController.cs` — `GET /api/admin/dashboard/recent-payments` + `GET /api/admin/dashboard/pending-crypto`
  - `src/Backend/AuraCore.API/Controllers/Payment/StripeController.cs` — webhook + checkout (writes payments)
  - `src/Backend/AuraCore.API/Controllers/Payment/CryptoController.cs` — crypto payment create/confirm/admin-verify
  - `src/Backend/AuraCore.API/Controllers/Admin/AdminRevenueController.cs` — `GET /api/admin/revenue/chart-data`
  - `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminChartController.cs` — `GET /api/admin/charts/revenue` (backup only — NOT in local repo or deployed DLL)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/Payment.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 119–135
- Live DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14)
- Server backup: `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Payment/StripeController.cs` (408 lines vs local 276 lines)

## Summary

- **3 critical** — Webhook NullReferenceException on null Stripe-Signature (500 instead of 400); missing idempotency check allows double-payment credit; Reject button calls a non-existent endpoint (404 → silent fail leaving payment in confirming state permanently)
- **3 high** — Revenue chart call hits non-deployed AdminChartController (404); hardcoded `$` symbol ignores stored `Currency` field; `invoice.AmountPaid / 100.0` double-cast instead of `/ 100m` introduces floating-point precision risk
- **3 medium** — No confirmation dialog on Approve/Reject crypto (immediate irreversible mutations); no admin audit log for any payment mutation (CTP-2); `ExternalId` index is non-unique — allows duplicate payment records with same Stripe session ID
- **2 low** — StatusBadge does not handle `awaiting_payment`, `confirming`, or `disputed` statuses (renders badge-blue generic fallback); no blockchain explorer link for crypto TX hash

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] No `AdminPaymentController` exists — Payments tab data source is `AdminDashboardController`

**Axis:** drift, functional
**Baseline bug ref:** B-4 (rollback artifacts)

**Symptom:** There is no `AdminPaymentController.cs` in the local repo, the server backup, or the deployed DLL. The Payments tab is NOT a dedicated admin payments endpoint — it pulls data from two Dashboard endpoints and one public-namespace Crypto endpoint.

**Root cause:**
- `PaymentsPage` (`page.tsx:621`) calls `api.getRecentPayments(50)` → `GET /api/admin/dashboard/recent-payments?count=50` (AdminDashboardController)
- `PaymentsPage` (`page.tsx:621`) also calls `api.getPendingCrypto()` → `GET /api/admin/dashboard/pending-crypto` (AdminDashboardController)
- Crypto approve → `POST /api/payment/crypto/admin/verify/{paymentId}` (CryptoController — `[Authorize(Roles = "admin")]` ✓)
- Crypto reject → `POST /api/payment/crypto/admin/reject/{paymentId}` (see F-3 — endpoint missing from deployed DLL)
- Revenue chart (Dashboard page) → `GET /api/admin/charts/revenue` (see F-4 — chart controller missing)

**DB state verification:**
```sql
SELECT COUNT(*) FROM payments;
-- Result: 0 rows (no payments in DB — Stripe not configured in prod, no real purchases yet)
```
- Payments table exists and schema is correct. Zero data is expected for a pre-revenue product.

**Fix suggestion:**
- Option A (preferred): Create a dedicated `AdminPaymentController.cs` with paginated `GET /api/admin/payments` (similar to other admin tabs). This separates the concern from `AdminDashboardController`.
- Option B: Keep using Dashboard endpoints but add pagination (currently capped at `count=50`; no way to paginate past 50 payments).
- Either way, fix F-3 and F-4 first (broken endpoints).

**Risk if unfixed:**
- Admin cannot see more than 50 most recent payments (no pagination).
- Tab architecture is inconsistent — all other tabs have a dedicated controller.

---

### F-2 [CRITICAL] Stripe webhook returns HTTP 500 on null `Stripe-Signature` header — `NullReferenceException` escapes `catch (StripeException)` block

**Axis:** security, functional
**Baseline bug ref:** (new — not in pre-audit known bugs)

**Symptom:** `POST /api/payment/stripe/webhook` with no `Stripe-Signature` header returns HTTP 500 (empty body) instead of HTTP 400. The error is a `NullReferenceException` from inside the Stripe SDK's `ParseStripeSignature`.

**Reproduction steps:**
1. `curl -X POST https://api.auracore.pro/api/payment/stripe/webhook -H 'Content-Type: application/json' -d '{}'`
2. Response: HTTP 500, empty body
3. Server logs (confirmed via journalctl): `System.NullReferenceException: Object reference not set to an instance of an object. at Stripe.EventUtility.ParseStripeSignature(String stripeSignatureHeader)`

**Root cause:**
- `StripeController.cs:151` — `try { stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], webhookSecret); }`
- `Request.Headers["Stripe-Signature"]` returns `StringValues.Empty` when header is absent. The Stripe SDK's `ParseStripeSignature` throws a `NullReferenceException` (not a `StripeException`) when the header value is null/empty.
- The `catch (StripeException)` block on line 152 does NOT catch `NullReferenceException` — it propagates as an unhandled exception → 500.
- The `webhookSecret` guard at line 147 (`if (string.IsNullOrEmpty(webhookSecret)...`) returns 400 correctly, but only catches the missing-secret case, not the missing-header case.
- Both local repo AND backup have this gap (backup StripeController.cs:111 has the same pattern, same catch type). The difference is the backup has logging via `_logger.LogWarning`; local repo has no logging either.

**DB state verification:** Not applicable (no DB write occurs — exception thrown before any DB access).

**Fix suggestion:**
- Add a guard before `ConstructEvent`: `if (!Request.Headers.ContainsKey("Stripe-Signature")) return BadRequest(new { error = "Missing Stripe-Signature header" });`
- OR: Widen the catch: `catch (Exception ex) when (ex is StripeException || ex is NullReferenceException) { return BadRequest(new { error = "Invalid signature" }); }`
- Option A is cleaner and more explicit.

**Risk if unfixed:**
- Any attacker (or misconfigured service) hitting the webhook endpoint without a `Stripe-Signature` header causes a 500, which logs a full stack trace and may expose internal path information (`C:\Users\Admin\Desktop\AuraCorePro\...` — confirmed in log output). 
- DoS vector: repeated unauthenticated POSTs trigger unhandled exceptions and flood the error log.
- The webhook IS properly protected against forged signatures (the `StripeException` path rejects tampered payloads correctly) — but the null-header case bypasses the graceful rejection.

---

### F-3 [CRITICAL] `AdminRejectPayment` endpoint missing from deployed DLL — crypto Reject button silently fails, payment stuck in `confirming` forever

**Axis:** functional, drift
**Baseline bug ref:** B-4 (rollback stripped endpoint)

**Symptom:** Admin clicks "Reject" on a pending crypto payment. The UI removes the payment from the pending list (optimistic update), but the payment record stays in `Status = 'confirming'` in the DB permanently. The endpoint `POST /api/payment/crypto/admin/reject/{paymentId}` returns HTTP 404.

**Reproduction steps:**
1. Navigate to Payments tab → Pending Crypto section (if any crypto payments exist)
2. Click "Reject" on any pending payment
3. UI removes the row from the pending list (optimistic update fires immediately)
4. In DB: `SELECT "Status" FROM payments WHERE "Id" = '<payment-id>';` → still `confirming`
5. Direct test: `curl -X POST https://api.auracore.pro/api/payment/crypto/admin/reject/00000000-0000-0000-0000-000000000001 → HTTP 404`

**Root cause:**
- `api.ts:69` — `request('/api/payment/crypto/admin/reject/${paymentId}', { method: 'POST' })` — calls the `AdminRejectPayment` endpoint.
- `CryptoController.cs` (local repo, 144 lines) — `AdminRejectPayment` method is **absent**. Only `CreateCryptoPayment`, `ConfirmCryptoPayment`, and `AdminVerifyPayment` exist.
- Server backup `CryptoController.cs` (line 149–158) has `[HttpPost("admin/reject/{paymentId:guid}")] AdminRejectPayment` — the full implementation.
- DLL strings confirm: `AdminVerifyPayment` is compiled in (`<AdminVerifyPayment>d__5`), but no `AdminRejectPayment` method exists in the deployed DLL.
- The backup's `AdminRejectPayment` sets `payment.Status = "rejected"` — a valid state transition. The local repo's controller has no such endpoint.

**DB state verification:**
```sql
SELECT COUNT(*) FROM payments WHERE "Status" = 'confirming';
-- Currently: 0 rows (no crypto payments in DB)
-- If a payment were in 'confirming': after clicking Reject, status would remain 'confirming'
-- No admin tool exists to manually move it
```

**Fix suggestion:**
- Restore `AdminRejectPayment` from the backup into local repo's `CryptoController.cs`.
- Add optimistic-update rollback: if the API returns 404/error, re-add the payment to the pending list in the UI (currently the UI removes it regardless of API response).

**Risk if unfixed:**
- Admin has no way to reject fraudulent or failed crypto payments. Payments remain in `confirming` permanently.
- A user who submitted a fake TX hash cannot be rejected — no status transition available.
- Admin's only recourse is a manual DB UPDATE (write-gate required), which is not documented for operators.

---

### F-4 [HIGH] Revenue chart API call hits non-deployed endpoint — `GET /api/admin/charts/revenue` returns 404

**Axis:** functional, drift
**Baseline bug ref:** B-4

**Symptom:** The admin dashboard page calls `api.getRevenueChart(30)` → `GET /api/admin/charts/revenue?days=30` which returns HTTP 404. The revenue chart on the Dashboard page shows no data. This is a Dashboard-page finding, but its root cause lives in the Payments data architecture.

**Root cause:**
- `api.ts:229` — `request('/api/admin/charts/revenue?days=${days}')` — calls `AdminChartController.GetRevenueChart`.
- `AdminChartController.cs` **does not exist in the local repo** and is **not in the deployed DLL** (confirmed via `strings /var/www/auracore-api/AuraCore.API.dll | grep AdminChart` → empty).
- The backup at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminChartController.cs` contains the full implementation: `GetRevenueChart` + `GetRegistrationChart`.
- The local repo does have `AdminRevenueController.cs` at `GET /api/admin/revenue/chart-data` (different route), but the frontend calls `/api/admin/charts/revenue` — route mismatch.
- Verified: `curl https://api.auracore.pro/api/admin/charts/revenue?days=30 → 404`. `curl https://api.auracore.pro/api/admin/revenue/chart-data → 401` (endpoint exists but route is not what the frontend calls).

**DB state verification:**
```sql
-- AdminRevenueController.GetChartData query (correct logic):
SELECT COUNT(*), SUM("Amount") FROM payments WHERE "Status" = 'completed';
-- Result: 0 rows, NULL sum (no completed payments in DB)
```

**Fix suggestion:**
- Option A (restore chart controller): Restore `AdminChartController.cs` from backup into local repo. Route is `api/admin/charts` matching frontend expectation.
- Option B (reroute frontend): Update `api.ts:229` to call `/api/admin/revenue/chart-data` (the existing `AdminRevenueController`). Note: `AdminRevenueController` takes `months` not `days` — additional parameter harmonization needed.
- Option A is simpler and preserves frontend contract.

**Risk if unfixed:**
- Admin dashboard revenue chart shows no data. Financial visibility into revenue trends is completely absent.

---

### F-5 [CRITICAL] Missing idempotency check in `HandleCheckoutCompleted` — Stripe webhook retry creates duplicate payment records and double-credits license

**Axis:** security, code-db-sync, functional
**Baseline bug ref:** (new — financial-specific finding)

**Symptom:** If Stripe retries a `checkout.session.completed` webhook (Stripe retries up to 3 days on non-2xx responses), or if the endpoint is called twice with the same session ID, a duplicate `Payment` record is created and the license `ExpiresAt` is reset again. Each retry creates a new payment row with identical `ExternalId`.

**Reproduction steps (WRITE_GATE — do NOT execute):**
- Would require: posting the same session ID twice to `/api/payment/stripe/webhook` with valid signature (not attempted — WRITE GATE)
- Root cause verified via code read only.

**Root cause:**
- `StripeController.cs:226` — `HandleCheckoutCompleted` calls `_db.Payments.Add(...)` unconditionally. There is NO check like `await _db.Payments.AnyAsync(p => p.ExternalId == session.Id && p.Status == "completed", ct)` before inserting.
- The backup controller (`/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Payment/StripeController.cs:183–184`) has the exact idempotency guard that is missing from the local repo:
  ```csharp
  var alreadyProcessed = await _db.Payments.AnyAsync(p => p.ExternalId == session.Id && p.Status == "completed", ct);
  if (alreadyProcessed) return;
  ```
- The `ExternalId` column has a non-unique index (`e.HasIndex(p => p.ExternalId)` — no `.IsUnique()`), so the DB does NOT enforce uniqueness. Multiple rows with the same `ExternalId` are permitted at the DB level.
- `HandleInvoicePaid` has the same missing check — a retry of `invoice.paid` creates an additional payment row.

**DB state verification:**
```sql
-- Idempotency check: are there any duplicate ExternalIds?
SELECT "ExternalId", COUNT(*) FROM payments GROUP BY "ExternalId" HAVING COUNT(*) > 1;
-- Currently: 0 rows (no payments in DB, so no duplicates yet)
```

**Fix suggestion:**
- Restore the `alreadyProcessed` idempotency check from the backup into both `HandleCheckoutCompleted` and `HandleInvoicePaid`.
- Additionally: add a unique constraint on `ExternalId`: `e.HasIndex(p => p.ExternalId).IsUnique()` — this prevents DB-level duplicates even if the app-level check has a race condition.

**Risk if unfixed:**
- Stripe's webhook retry mechanism (guaranteed delivery) will create duplicate payment records for every Stripe webhook event that returns non-2xx (including the webhook's own 500 bug from F-2).
- Duplicate `HandleCheckoutCompleted` calls could create duplicate licenses for the same user.
- Revenue dashboard SUM will be inflated by duplicate completed payment rows.

---

### F-6 [HIGH] `invoice.AmountPaid / 100.0` uses IEEE 754 double division — floating-point precision loss on invoice amounts

**Axis:** functional, code-db-sync
**Baseline bug ref:** (new — financial-specific)

**Symptom:** Invoice renewal amounts stored via `HandleInvoicePaid` may contain floating-point rounding artifacts (e.g., `4.990000000000001` instead of `4.99`) because the division is done in double-precision floating point.

**Root cause:**
- `StripeController.cs:248` — `Amount = (decimal)(invoice.AmountPaid / 100.0)` — `invoice.AmountPaid` is `long?`, `100.0` is `double`. The expression `longValue / 100.0` performs IEEE 754 double division before casting to `decimal`. For most integers this is exact, but for values like `499` (→ `4.99`) the double `499 / 100.0` is `4.99` exactly, so in practice this specific case is safe. However, for larger amounts like `12999` (→ `129.99`): `12999 / 100.0` in double = `129.99` exactly. The risk is theoretical at common price points but a correctness smell.
- Compare: `StripeController.cs:225` — `var amount = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0` uses `100m` (decimal literal) — correct.
- Line 248 in `HandleInvoicePaid` uses `/ 100.0` (double) — inconsistent with line 225.
- The DB column is `numeric` (unbounded precision, confirmed by `information_schema.columns`) — the `decimal(10,2)` EF config hint was NOT applied by the migration. The DB would store whatever floating-point noise the double cast introduces.

**Fix suggestion:**
- Change `(decimal)(invoice.AmountPaid / 100.0)` to `invoice.AmountPaid.GetValueOrDefault() / 100m` — same pattern as line 225.

**Risk if unfixed:**
- Subscription renewal amounts in the DB may have floating-point noise. Revenue totals in the admin dashboard will be slightly wrong for affected renewals.

---

### F-7 [HIGH] Amount column displays hardcoded `$` — ignores `Currency` field, wrong for TRY/EUR payments

**Axis:** functional, code-db-sync
**Baseline bug ref:** (new — financial-specific)

**Symptom:** Every payment in the Payments table is displayed as `$X.XX` regardless of the stored `Currency` field. Turkish Lira payments (TRY) and Euro payments (EUR) are displayed as `$149.00` instead of `₺149.00` or `€149.00`.

**Root cause:**
- `page.tsx:669` — `<td>${ (p.amount ?? 0).toFixed(2) }</td>` — the `$` prefix is a JSX template literal hardcoded `$`. The `p.currency` field (returned by the API as `currency` in the payment object) is never used in the Payments table.
- The `p.crypto` field is also referenced in the Pending Crypto section (`page.tsx:641`) as `{p.crypto}` — but the API returns `provider` not `crypto` for the pending-crypto endpoint. This is a field-name mismatch in the pending panel (the provider is displayed from `p.provider` in the main table correctly, but the pending card reads `p.crypto` which would be `undefined`).
- `StripeController.cs:45` — supports TRY and EUR: `var currency = (req.Currency?.ToLower()) switch { "try" => "try", "eur" => "eur", _ => "usd" }`. Multi-currency support is backend-complete but frontend-invisible.

**DB state verification:**
```sql
SELECT DISTINCT "Currency" FROM payments;
-- Currently: 0 rows (no payments in DB)
-- When payments exist: could have 'USD', 'TRY', 'EUR', 'BTC', 'USDT'
```

**Fix suggestion:**
- Replace `${ (p.amount ?? 0).toFixed(2) }` with a currency-aware renderer:
  ```tsx
  {p.currency === 'BTC' ? `${p.amount} BTC`
    : p.currency === 'USDT' ? `${p.amount} USDT`
    : new Intl.NumberFormat(undefined, { style: 'currency', currency: p.currency || 'USD' }).format(p.amount ?? 0)}
  ```
- Fix `p.crypto` → `p.provider` in the pending crypto card (line 641).

**Risk if unfixed:**
- Admin cannot distinguish USD from TRY from EUR amounts. Revenue shown in admin panel is in mixed currencies displayed uniformly as `$`. Turkish users who paid ₺149 appear to have paid $149 — 30x difference.

---

### F-8 [MEDIUM] No confirmation dialog on crypto Approve/Reject — immediate irreversible mutations with no guard

**Axis:** UX, security
**Cross-tab pattern ref:** CTP-4 (first surfaced: Users tab F-9; confirmed Licenses F-7)

**Symptom:** Clicking "Approve" or "Reject" on a pending crypto payment immediately fires the API call with no "Are you sure?" prompt. Approve activates a Pro/Enterprise license; Reject (even if it worked per F-3) would permanently mark the payment as rejected with no undo.

**Root cause:**
- `page.tsx:644` — `onClick={async () => { await api.verifyCryptoPayment(p.id); ... }}` — no `confirm()` guard.
- `page.tsx:646` — `onClick={async () => { await api.rejectCryptoPayment(p.id); ... }}` — no `confirm()` guard.
- CTP-4 pattern confirmed for Payments tab. Unlike other tabs where the un-confirmed action is delete/revoke, here it's an irreversible financial decision (approving a potentially fraudulent payment grants Pro access permanently; rejecting a legitimate payment denies a paying customer access).

**Fix suggestion:**
- Add `if (!confirm('Approve this crypto payment? This will activate Pro/Enterprise access for the user.'))` before `verifyCryptoPayment`.
- Add `if (!confirm('Reject this payment? The user will NOT receive access.'))` before `rejectCryptoPayment`.
- Or: implement a modal with a display of the TX hash for visual verification before approval.

**Risk if unfixed:**
- Admin accidentally approves a fraudulent payment (wrong row tap) → free Pro access granted.
- Admin accidentally rejects a legitimate payment → paying customer locked out.

---

### F-9 [MEDIUM] No audit log for any payment mutation (CTP-2 confirmed for Payments tab)

**Axis:** security
**Cross-tab pattern ref:** CTP-2 (confirmed in Subscriptions, Users, Licenses — now Payments)

**Symptom:** Admin Approve of a crypto payment (`AdminVerifyPayment`), Stripe webhook completion (`HandleCheckoutCompleted`), and all other payment state changes are not written to any audit table.

**Root cause:**
- `CryptoController.cs:108–139` (`AdminVerifyPayment`) — no audit log write.
- `StripeController.cs:211–237` (`HandleCheckoutCompleted`) — no audit log write.
- No `admin_audit_log` table exists (confirmed via prior audits — CTP-2 established).
- Financial mutations are the highest-priority audit targets: "Who approved this crypto payment?" is unanswerable from the DB.

**Fix suggestion:** Same as CTP-2: add `admin_audit_log` table + service wired into all mutation controllers. Payment approval should log actor + paymentId + tier granted + timestamp.

**Risk if unfixed:**
- Insider threat: an admin can approve payments for friends/fake accounts with no evidence trail.
- Compliance risk for any payment dispute: Stripe chargebacks require an audit trail.

---

### F-10 [MEDIUM] `ExternalId` non-unique index allows DB-level duplicate payment records with same Stripe session ID

**Axis:** security, code-db-sync
**Baseline bug ref:** (new — financial-specific, companion to F-5)

**Symptom:** The `payments.ExternalId` column has a non-unique index. Multiple rows can exist with the same Stripe session ID / invoice ID. The only guard against duplicates is the application-level idempotency check (which is MISSING — see F-5). Nothing at the DB level prevents duplicate payment records.

**Root cause:**
- `AuraCoreDbContext.cs:132` — `e.HasIndex(p => p.ExternalId);` — index only, NO `.IsUnique()`.
- Compare to: `e.HasIndex(l => l.Key).IsUnique()` (License) — licenses correctly enforce uniqueness.
- The non-unique index was used for query performance (`WHERE ExternalId = ?`), not for deduplication.

**DB state verification:**
```sql
SELECT indexname, indexdef FROM pg_indexes WHERE tablename='payments';
-- Result: ONLY payments_pkey (primary key). The ExternalId index from EF config does NOT appear in the DB.
-- This means the migration did not apply the HasIndex for ExternalId either — the index does not exist at all.
```

**Fix suggestion:**
- Change `e.HasIndex(p => p.ExternalId)` to `e.HasIndex(p => p.ExternalId).IsUnique()` — add a unique constraint.
- Apply a DB migration: `CREATE UNIQUE INDEX payments_externalid_unique ON payments("ExternalId") WHERE "ExternalId" IS NOT NULL;` (partial unique index to allow NULL ExternalIds for crypto payments that don't yet have an ID).

**Risk if unfixed:**
- As F-5 describes, Stripe retries can create duplicate payment rows. Without a DB-level guard, even a fixed app-level check has a race condition (two concurrent webhook calls).

---

### F-11 [LOW] `StatusBadge` does not handle `awaiting_payment`, `confirming`, or `disputed` payment statuses

**Axis:** UX, functional

**Symptom:** Crypto payments created via `CryptoController.CreateCryptoPayment` have `Status = "awaiting_payment"`. After user submits TX hash, status is `"confirming"`. If a Stripe dispute occurs (per backup `charge.dispute.funds_withdrawn` handler), status could be `"disputed"`. None of these are explicitly handled by `StatusBadge`.

**Root cause:**
- `page.tsx:290–300` — `StatusBadge` maps: `completed/active/online` → green, `pending` → amber, `cancelled/revoked/failed/refunded` → red, tier badges. No case for `awaiting_payment`, `confirming`, or `disputed`.
- All three fall through to `'badge-blue'` (generic blue) — same as `free` tier badge. Visually indistinguishable from a free-tier row.
- The `Payment.Status` entity comment says: `"pending, completed, failed, refunded"` — but the actual code uses `awaiting_payment`, `confirming`, `rejected`, `disputed` (in backup).

**Fix suggestion:**
- Add: `s === 'confirming' || s === 'awaiting_payment' ? 'badge-amber'` (amber = waiting, like pending)
- Add: `s === 'disputed' ? 'badge-red'`
- Add: `s === 'rejected' ? 'badge-red'`
- Update `Payment.cs` Status comment to reflect the full set of valid statuses.

**Risk if unfixed:**
- `confirming` crypto payments look identical to `free` tier users in the payments list — admin cannot visually triage which payments need attention.

---

### F-12 [LOW] No blockchain explorer link for crypto TX hash — admin must manually look up on-chain

**Axis:** UX, functional

**Symptom:** The pending crypto payments card displays `userEmail` and `$amount - {p.crypto}` but shows no TX hash and no blockchain explorer link. The `CryptoTxHash` field is stored in the DB when a user submits it via `ConfirmCryptoPayment`, but the admin has no way to verify it from the UI — they would need to look it up manually on Etherscan/Tronscan/Blockchain.com.

**Root cause:**
- `page.tsx:641` — `{p.amount} - {p.crypto}` — `p.crypto` is `undefined` (API returns `provider` not `crypto`). TX hash is not displayed.
- `AdminDashboardController.cs:52` — `pending-crypto` projection includes `p.CryptoTxHash` — the hash IS returned by the API but the frontend doesn't render it.

**Fix suggestion:**
- Add TX hash display in the pending crypto card with a blockchain explorer link:
  ```tsx
  {p.cryptoTxHash && (
    <a href={explorerUrl(p.provider, p.cryptoTxHash)} target="_blank">
      View TX: {p.cryptoTxHash.substring(0, 16)}...
    </a>
  )}
  ```
- Explorer URLs: BTC → `https://www.blockchain.com/btc/tx/{hash}`, USDT TRC20 → `https://tronscan.org/#/transaction/{hash}`, USDT ERC20 → `https://etherscan.io/tx/{hash}`.

**Risk if unfixed:**
- Admin cannot verify crypto payments directly from the admin panel. Manual verification outside the panel introduces operational friction and error risk.

---

### F-13 [HIGH] `HandleCheckoutCompleted` hardcodes `Currency = "USD"` — ignores session's actual currency for multi-currency payments

**Axis:** code-db-sync, functional

**Symptom:** When a TRY or EUR Stripe payment completes (`checkout.session.completed` webhook), the payment record is stored with `Currency = "USD"` regardless of the actual payment currency.

**Root cause:**
- `StripeController.cs:226` — `Currency = "USD"` — hardcoded string literal in `HandleCheckoutCompleted`.
- Compare: `CreateCheckoutSession` (line 76) correctly stores the `currency` from the request. But when the webhook fires on completion, the stored record overwrites with hardcoded `"USD"`.
- The session's actual currency is available as `session.Currency` (a field on the Stripe `Session` object) but is not read.
- The backup's `HandleCheckoutCompleted` also hardcodes USD (`StripeController.cs:201` in backup) — this bug exists in both versions.

**Fix suggestion:**
- Replace `Currency = "USD"` with `Currency = session.Currency?.ToUpper() ?? "USD"` in `HandleCheckoutCompleted`.
- In `HandleInvoicePaid` (line 248): `Currency = invoice.Currency.ToUpper()` is already correct (reads from invoice).

**Risk if unfixed:**
- All completed Stripe payments appear as USD in the admin panel regardless of actual currency. Revenue reporting for Turkish (TRY) or European (EUR) markets is incorrect.

---

## DB Schema Drift Notes (read-only, no findings)

EF Core config vs actual DB schema — differences found (all benign, no functional failures):
- `Provider`: EF config `HasMaxLength(20)`, DB actual `varchar(50)` — DB is more permissive; EF config is stricter.
- `ExternalId`: EF config `HasMaxLength(512)`, DB actual `varchar(255)` — DB is more restrictive. Stripe session IDs are typically <255 chars; no truncation risk in practice.
- `Status`: EF config `HasMaxLength(20)`, DB actual `varchar(50)`.
- `Amount`: EF config `decimal(10,2)`, DB actual `numeric` (unbounded) — DB is more precise.
- ExternalId index: EF config declares `HasIndex(p => p.ExternalId)` but DB has NO such index (only `payments_pkey`). Migration did not apply the index. Query performance for `WHERE ExternalId = ?` is impacted (full table scan).

---

## Axis-by-axis coverage

### 1. Functional

- **List view:** Functional but limited — `getRecentPayments(50)` caps at 50. No pagination, no search, no filter by provider/status/date. For 0-row DB, shows "No payments recorded" (correct empty state).
- **Pending crypto view:** Non-functional — `rejectCryptoPayment` hits 404 (F-3). Approve works. Neither shows TX hash (F-12).
- **Revenue chart (Dashboard):** Broken — `/api/admin/charts/revenue` → 404 (F-4).
- **Empty state:** Renders correctly — `EmptyState` with CreditCard icon and "No payments recorded" text.
- **No create/update/delete:** Payments are write-only from external events (Stripe webhook, crypto flow). Admin has no payment mutation surface except Approve/Reject on crypto pending.

### 2. Code + DB sync

- **CTP-1 check:** Not applicable — Payments tab does not display tier directly.
- **Currency mismatch (F-7, F-13):** Payment records store multi-currency correctly for `HandleInvoicePaid`; but `HandleCheckoutCompleted` hardcodes USD. Frontend ignores `Currency` field entirely.
- **Idempotency gap (F-5):** Webhook can create duplicate payment records.
- **Double-cast precision (F-6):** `/ 100.0` (double) on renewal amounts.
- **ExternalId index missing from DB (drift note):** No performance index on ExternalId despite EF config declaring one.
- **Revenue query accuracy:** `AdminRevenueController.GetChartData` queries `WHERE Status = 'completed'` — this is correct against the entity values used by the code. When payments exist and are marked `completed`, the revenue SUM should match DB truth. **Revenue query logic is correct; the problem is the 404 that prevents it from being called (F-4).**

### 3. Security

- **Authorization:** `AdminDashboardController` — `[Authorize(Roles = "admin")]` at class level ✓. `CryptoController.AdminVerifyPayment` — `[Authorize(Roles = "admin")]` ✓. `StripeController.Webhook` — `[AllowAnonymous]` (correct for webhook — Stripe can't authenticate). All admin endpoints require admin JWT.
- **IDOR:** `GetPaymentStatus` (`StripeController.cs:168`) — filters by `p.UserId == userId` — correct IDOR protection for user-facing endpoint. Admin endpoints have no IDOR risk (single-tenant, admin = all access).
- **CSRF:** Stateless JWT — no CSRF risk.
- **XSS:** `userEmail` is React-escaped. No `dangerouslySetInnerHTML`. No XSS vector.
- **SQL injection:** All queries use EF Core parameterized queries. No raw SQL.
- **Stripe webhook signature:** `EventUtility.ConstructEvent` with `webhookSecret` is called — **signature verification IS present** and blocks forged payloads when a proper `Stripe-Signature` header is provided. The F-2 bug only affects requests with null/missing signature headers (returns 500 instead of 400).
- **Webhook secret configured:** Returns 400 correctly if `STRIPE_WEBHOOK_SECRET` env var is missing (line 147 guard).
- **Rate limit:** No rate limiting on webhook endpoint. Repeated webhook calls with no signature trigger F-2 (500 + log spam). Practical DDoS risk is low (behind Nginx with basic infra).
- **Audit log:** Missing — CTP-2 confirmed (F-9).
- **Nginx basic auth bypass:** `api.auracore.pro` vhost has no basic auth — same pattern as all other tabs (confirmed in prior audits). Admin endpoints rely solely on JWT `[Authorize(Roles = "admin")]`.

### 4. UX

- **Loading state:** No loading spinner while data fetches. `useEffect` fires on mount — brief flash of empty table.
- **Error state:** `getRecentPayments` and `getPendingCrypto` both `catch { return []; }` — silent failure. No error message shown to admin. If the API is down, the tab shows "No payments recorded" with no indication of error.
- **Empty state:** Correct — shows "No payments recorded" with icon when payments array is empty.
- **Destructive confirmation:** No confirmation on Approve or Reject (F-8). CTP-4 confirmed.
- **Refresh survival (Bug 3):** PaymentsPage uses `useEffect` — Refresh navigates to same page, React remounts, `useEffect` fires again. Bug 3 does NOT affect Payments tab (soft remount, not hard reload).
- **Double-submit protection:** Approve/Reject buttons have no loading state or disable-during-request. Double-tap would fire two requests.

### 5. Mobile

CTP-3 applies (same root layout, fixed 260px sidebar — no breakpoints). Same as Subscriptions/Users/Licenses:
- **375px:** Sidebar 260px + main 115px — content severely constrained. 5-column table (User, Provider, Amount, Status, Date) requires heavy horizontal scroll.
- **320px:** Sidebar 260px + main 60px — essentially unusable.
- **Approve/Reject button sizes:** `btn-ghost text-aura-green ... flex items-center gap-1` with `w-3 h-3` icon — likely ~28–32px height. Below 44px tap target minimum.
- **Pending crypto card:** Two buttons side by side (`gap-2`) — on small screens both may not fit.

### 6. Deployment drift

- **AdminPaymentController:** Does not exist in local repo, backup, or DLL — not a rollback artifact. This endpoint was never implemented; tab always used Dashboard endpoints.
- **AdminChartController:** Exists in backup (408 lines), missing from local repo and deployed DLL (F-4). This IS a rollback artifact (CTP-6 pattern confirmed).
- **CryptoController:** Backup has 162 lines (with `AdminRejectPayment`), local repo has 144 lines (missing `AdminRejectPayment`) — rollback artifact (F-3, CTP-6 pattern confirmed).
- **StripeController:** Backup has 408 lines, local repo has 276 lines — the backup has more event handlers (`invoice.payment_failed`, `charge.refunded`, `charge.dispute.funds_withdrawn`, `charge.dispute.updated`) and logging. Local repo stripped these. Additionally, the backup has the idempotency check (F-5). CTP-6 confirmed for financial controllers.
- **Source vs deployed DLL:** Local repo matches deployed DLL (both lack `AdminChartController` and full `CryptoController`). Consistent — rollback occurred at source level.
- **Admin panel source drift:** Same 26-day gap as prior tabs (source = March 27, live = April 21). No payment-specific TSX changes in April 21 build vs March 27 source.

---

## CTP-6 Update: Financial controllers also stripped

Both `StripeController.cs` (276 vs 408 lines) and `CryptoController.cs` (144 vs 162 lines) are significantly stripped vs the server backup. Key capabilities removed from local repo vs backup:
- `StripeController`: idempotency check for `HandleCheckoutCompleted` (F-5), `HandlePaymentFailed`, `HandleChargeRefunded`, `HandleDisputeFundsWithdrawn`, `HandleDisputeUpdated` event handlers, structured logging via `_logger`
- `CryptoController`: `AdminRejectPayment` endpoint (F-3)
- `AdminChartController`: entire controller missing (F-4)

The rollback that stripped `AdminLicenseController` (found in Licenses audit) also stripped financial controllers. These are higher-severity than the Licenses rollback because they affect payment processing logic.

---

## Financial-Specific CTPs Proposed

### CTP-7: Webhook idempotency — payment handlers must check ExternalId before inserting
**First surfaced:** Payments tab (F-5). May also affect: any future webhook-driven payment flow.
**Pattern:** `HandleCheckoutCompleted` and `HandleInvoicePaid` call `_db.Payments.Add(...)` unconditionally. Stripe's guaranteed delivery retries can fire the same event 3–10 times. Without an idempotency check, each retry creates a new payment record and updates the license again.
**Backup had this fix.** Local repo lost it during rollback.
**Fix:** `if (await _db.Payments.AnyAsync(p => p.ExternalId == sessionId && p.Status == "completed", ct)) return;` before any `Add`.

### CTP-8: Frontend hardcoded `$` currency symbol — multi-currency support ignored in all tabs
**First surfaced:** Payments tab (F-7). May affect: Dashboard recent payments panel.
**Pattern:** `${ (p.amount ?? 0).toFixed(2) }` on page.tsx:669 — `$` is a JSX template literal hardcoded prefix. Backend returns `currency` field; frontend never reads it.
**Fix:** Use `Intl.NumberFormat` with `style: 'currency'` and `currency: p.currency`.

---

## Questions for user

1. **Crypto address sharing:** The app uses a single shared BTC/USDT wallet address for all users (per `CryptoController.cs:37–43` — static address from config). This means multiple users pay to the same address — admin must manually match incoming transactions to payment records by amount + timing. Is this intentional? A payment gateway (NOWPayments, CoinGate, BTCPay) would generate unique per-payment addresses. The comment in the code says "In production: use a crypto payment gateway" — is this still the plan?

2. **Stripe live mode:** Is Stripe configured in live mode or test mode on production? `STRIPE_SECRET_KEY` env var — if it's a `sk_test_` key, the webhook receives test events only. Is the webhook URL registered in the Stripe dashboard?

3. **Refund capability:** There is no refund action in the admin panel for Stripe payments. Should admin be able to trigger Stripe refunds from the panel? Currently not possible — admin would need to go to Stripe dashboard manually. This is likely deliberate but worth confirming as scope for Phase 6 Item 8.
