/**
 * Audit Log page — native audit_log columns (actor / action / target / ip / time)
 * with KPI cards (total / last 24h / last 7d / top action) and debounced
 * actor-email search. Phase 6.10 W5.T23 final redesign.
 *
 * Replaces the Phase 6.9 transform-layer hack: the previous body rendered a
 * synthetic login_attempts shape (success boolean column, etc.) produced by
 * `api.getLoginAttempts()` reshaping audit_log rows. That adapter is gone —
 * we now call `api.getAuditLog()` / `api.getAuditLogStats()` and render the
 * backend rows verbatim per the AuditLogEntry interface.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-8 + 10 follow the same
 * convention).
 *
 * History: W2.T9 extracted from page.tsx; W2.T11 KPICard + EmptyState lifted;
 * W3.T17 minimum-swept to DataTable while preserving login_attempts shape;
 * W5.T23 redesigns the body per the plan's D6 spec (this commit).
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import { Activity, Clock, FileText, RefreshCw, Tag, TrendingUp } from 'lucide-react';
import { api } from '@/lib/api';
import { AuditLogEntry, ListResponse } from '@/lib/types';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { PaginationLabel } from '@/components/PaginationLabel';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';

const columns: DataTableColumn<AuditLogEntry>[] = [
    {
        key: 'actor',
        header: 'Actor',
        isCardTitle: true,
        render: (e) => <span className="text-white/85 font-mono text-xs">{e.actorEmail || '—'}</span>,
    },
    {
        key: 'action',
        header: 'Action',
        render: (e) => <span className="badge badge-cyan">{e.action}</span>,
    },
    {
        key: 'target',
        header: 'Target',
        render: (e) => (
            <span className="text-white/55 font-mono text-xs">
                {e.targetType}
                {e.targetId ? `/${e.targetId.length > 8 ? `${e.targetId.substring(0, 8)}…` : e.targetId}` : ''}
            </span>
        ),
    },
    {
        key: 'ip',
        header: 'IP',
        render: (e) => <span className="text-white/40 font-mono text-xs">{e.ipAddress ?? '—'}</span>,
    },
    {
        key: 'time',
        header: 'Time',
        render: (e) => <span className="text-white/40 font-mono text-xs">{new Date(e.createdAt).toLocaleString()}</span>,
    },
];

interface AuditLogStats {
    total?: number;
    last24h?: number;
    today?: number;
    last7d?: number;
    thisWeek?: number;
    topActions?: Array<{ action: string; count: number }>;
}

export function AuditLogPage() {
    const [data, setData] = useState<ListResponse<AuditLogEntry>>({
        total: 0, page: 1, pageSize: 50, pages: 0, items: [],
    });
    const [stats, setStats] = useState<AuditLogStats | null>(null);
    const [search, setSearch] = useState('');
    const debouncedSearch = useDebouncedValue(search, 400);
    const [page, setPage] = useState(1);

    // Reset to page 1 whenever the actor-email filter changes.
    useEffect(() => { setPage(1); }, [debouncedSearch]);

    const load = useCallback(async () => {
        const [d, s] = await Promise.all([
            api.getAuditLog(debouncedSearch || undefined, undefined, page),
            api.getAuditLogStats(),
        ]);
        setData(d);
        setStats(s);
    }, [debouncedSearch, page]);

    useEffect(() => { load(); }, [load]);

    const topAction = stats?.topActions?.[0]?.action ?? '—';

    return (
        <div className="animate-fade-in">
            <PageHeader
                title="Audit Log"
                subtitle={`${data.total} mutation${data.total === 1 ? '' : 's'} recorded`}
                breadcrumb="~/admin/audit_log"
            >
                <button onClick={load} className="btn-ghost flex items-center gap-2">
                    <RefreshCw className="w-4 h-4" />Refresh
                </button>
            </PageHeader>

            {stats && (
                <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
                    <KPICard label="Total" value={stats.total ?? 0} icon={Activity} color="text-accent" />
                    <KPICard label="Last 24h" value={stats.last24h ?? stats.today ?? 0} icon={Clock} color="text-aura-blue" />
                    <KPICard label="Last 7d" value={stats.last7d ?? stats.thisWeek ?? 0} icon={TrendingUp} color="text-aura-green" />
                    <KPICard label="Top Action" value={topAction} icon={Tag} color="text-aura-purple" />
                </div>
            )}

            <div className="glass-card p-5">
                <div className="flex items-center gap-4 mb-5 flex-wrap">
                    <input
                        type="text"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        placeholder="Filter by actor email..."
                        className="input-dark w-full max-w-xs"
                    />
                </div>
                <DataTable<AuditLogEntry>
                    columns={columns}
                    rows={data.items}
                    rowKey={(e) => String(e.id)}
                    emptyState={<EmptyState icon={FileText} title="No audit log entries" />}
                />
                <div className="mt-4 flex justify-between items-center">
                    <PaginationLabel page={data.page} pageSize={data.pageSize} total={data.total} />
                    {data.pages > 1 && (
                        <div className="flex items-center gap-2">
                            <button
                                onClick={() => setPage(p => Math.max(1, p - 1))}
                                disabled={data.page <= 1}
                                className="btn-ghost px-3 py-1.5 text-xs disabled:opacity-30"
                            >
                                Prev
                            </button>
                            <span className="text-xs text-white/40 px-2">{data.page} / {data.pages}</span>
                            <button
                                onClick={() => setPage(p => Math.min(data.pages, p + 1))}
                                disabled={data.page >= data.pages}
                                className="btn-ghost px-3 py-1.5 text-xs disabled:opacity-30"
                            >
                                Next
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
