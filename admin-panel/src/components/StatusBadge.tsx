/**
 * Shared status badge primitive (Phase 6.10 W2.T11 — lifted from page.tsx /
 * DashboardPage / UsersPage / LicensesPage / PaymentsPage / UpdatesPage).
 *
 * Maps a free-form status string (case-insensitive) to one of the project's
 * `badge-*` Tailwind utility classes:
 *
 *   - active / completed / online    → badge-green
 *   - pending                        → badge-amber
 *   - cancelled / revoked / failed   → badge-red
 *   - refunded                       → badge-red
 *   - pro                            → badge-cyan
 *   - enterprise                     → badge-purple
 *   - admin                          → badge-red
 *   - free                           → badge-blue
 *   - <anything else>                → badge-blue (fallback)
 *
 * Tier strings (`pro`, `enterprise`, `free`) are intentionally handled here
 * so `TierBadge` (still inline at LicensesPage / UsersPage as a 1-line
 * StatusBadge wrapper) keeps its current behavior. TierBadge itself stays
 * inline — Wave 3 visual sweep will decide its fate.
 */
export interface StatusBadgeProps {
    status: string;
}

export function StatusBadge({ status }: StatusBadgeProps) {
    const s = status.toLowerCase();
    const cls = s === 'active' || s === 'completed' || s === 'online' ? 'badge-green'
        : s === 'pending' ? 'badge-amber'
        : s === 'cancelled' || s === 'revoked' || s === 'failed' || s === 'refunded' ? 'badge-red'
        : s === 'pro' ? 'badge-cyan'
        : s === 'enterprise' ? 'badge-purple'
        : s === 'admin' ? 'badge-red'
        : s === 'free' ? 'badge-blue'
        : 'badge-blue';
    return <span className={`badge ${cls}`}>{status}</span>;
}
