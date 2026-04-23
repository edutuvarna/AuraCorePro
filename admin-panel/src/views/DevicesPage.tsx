/**
 * Devices page — registered hardware list across all licenses with KPIs,
 * search, and pagination (Phase 6.10 W2.T7 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-6 + 7-10 follow the same
 * convention).
 *
 * KPICard + EmptyState lifted in W2.T11 to shared `@/components/`. SearchBar
 * and Pagination remain inline — they're outside the plan's primitive lift
 * list and Wave 3 Task 16 (DataTable conversion) will absorb them.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Monitor, Activity, TrendingUp, Plus, RefreshCw, Search,
    ChevronLeft, ChevronRight,
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { EmptyState } from '@/components/EmptyState';

// Inline (not in plan's lift list — Wave 3 / Task 16 will absorb).
function SearchBar({ value, onChange, placeholder = 'Search...', onSubmit }: {
    value: string; onChange: (v: string) => void; placeholder?: string; onSubmit?: () => void;
}) {
    return (
        <div className="relative">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-white/25" />
            <input type="text" value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
                onKeyDown={e => e.key === 'Enter' && onSubmit?.()}
                className="input-dark w-full pl-10" />
        </div>
    );
}

// Inline (not in plan's lift list — Wave 3 / Task 16 will absorb).
function Pagination({ page, pages, onChange }: { page: number; pages: number; onChange: (p: number) => void }) {
    if (pages <= 1) return null;
    return (
        <div className="flex items-center justify-center gap-2 mt-6">
            <button onClick={() => onChange(page - 1)} disabled={page <= 1} className="btn-ghost px-3 py-1.5 disabled:opacity-30">
                <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-sm text-white/40 px-3">{page} / {pages}</span>
            <button onClick={() => onChange(page + 1)} disabled={page >= pages} className="btn-ghost px-3 py-1.5 disabled:opacity-30">
                <ChevronRight className="w-4 h-4" />
            </button>
        </div>
    );
}

export function DevicesPage() {
    const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
    const [stats, setStats] = useState<any>(null);
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);

    const load = useCallback(async () => {
        const [d, s] = await Promise.all([api.getDevices(search || undefined, page), api.getDeviceStats()]);
        setData(d); setStats(s);
    }, [search, page]);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Devices" subtitle="Registered hardware across all licenses">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
                <KPICard label="Total Devices" value={stats?.totalDevices ?? data.total ?? 0} icon={Monitor} color="text-accent" />
                <KPICard label="Active Today" value={stats?.activeToday ?? 0} icon={Activity} color="text-aura-green" />
                <KPICard label="Active This Week" value={stats?.activeThisWeek ?? 0} icon={TrendingUp} color="text-aura-amber" />
                <KPICard label="New This Week" value={stats?.newThisWeek ?? 0} icon={Plus} color="text-aura-purple" />
            </div>

            <div className="glass-card p-5">
                <div className="mb-5 max-w-sm">
                    <SearchBar value={search} onChange={setSearch} placeholder="Search machine name or OS..." onSubmit={load} />
                </div>
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Machine</th>
                        <th className="text-left py-3 px-4 font-medium">OS</th>
                        <th className="text-left py-3 px-4 font-medium">Crashes</th>
                        <th className="text-left py-3 px-4 font-medium">Telemetry</th>
                        <th className="text-left py-3 px-4 font-medium">Last Seen</th>
                    </tr></thead>
                    <tbody>
                        {(data.items || []).map((d: any) => (
                            <tr key={d.id} className="table-row">
                                <td className="py-3 px-4"><div className="flex items-center gap-2"><Monitor className="w-4 h-4 text-white/30" /><span className="text-white/80">{d.machineName}</span></div></td>
                                <td className="py-3 px-4 text-white/50 text-xs">{d.osVersion}</td>
                                <td className="py-3 px-4 text-white/50">{d.crashCount ?? 0}</td>
                                <td className="py-3 px-4 text-white/50">{d.telemetryCount ?? 0}</td>
                                <td className="py-3 px-4 text-white/40">{d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleDateString() : '-'}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(data.items || []).length === 0 && <EmptyState icon={Monitor} title="No devices registered yet" subtitle="Devices will appear after users login from the desktop app" />}
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>
        </div>
    );
}
