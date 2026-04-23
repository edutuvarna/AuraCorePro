/**
 * Shared empty-state primitive (Phase 6.10 W2.T11 — lifted from
 * DashboardPage / UsersPage / LicensesPage / DevicesPage / CrashReportsPage /
 * TelemetryPage / AuditLogPage / PaymentsPage / UpdatesPage / IpWhitelistPage).
 *
 * Renders a centered icon-in-rounded-square + title + optional subtitle,
 * intended for "no rows match" / "nothing here yet" placeholders inside
 * tables and lists.
 *
 * Behavior is unchanged from the inline-code Phase 6.9 hotfix versions.
 */
export interface EmptyStateProps {
    icon: any;
    title: string;
    subtitle?: string;
}

export function EmptyState({ icon: Icon, title, subtitle }: EmptyStateProps) {
    return (
        <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="w-14 h-14 rounded-2xl bg-white/[0.03] border border-white/[0.06] flex items-center justify-center mb-4">
                <Icon className="w-6 h-6 text-white/20" />
            </div>
            <p className="text-white/40 font-medium">{title}</p>
            {subtitle && <p className="text-white/25 text-sm mt-1">{subtitle}</p>}
        </div>
    );
}
