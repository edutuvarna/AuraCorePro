/**
 * Users page — list/search/revoke-subscription/delete admin users
 * (Phase 6.10 W2.T5 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4 + 6-10 follow the same
 * convention).
 *
 * StatusBadge + EmptyState lifted in W2.T11 to shared `@/components/`.
 * SearchBar + Pagination + TierBadge remain inline — outside the plan's
 * primitive lift list (TierBadge is just a 1-line StatusBadge wrapper;
 * SearchBar/Pagination wait for Wave 3 Task 16 DataTable conversion).
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Users, Search, RefreshCw, Trash2, Ban, ChevronLeft, ChevronRight
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
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

// Inline (1-line wrapper around StatusBadge — kept for now per Task 11 spec).
function TierBadge({ tier }: { tier: string }) {
    return <StatusBadge status={tier} />;
}

export function UsersPage() {
    const [users, setUsers] = useState<any[]>([]);
    const [total, setTotal] = useState(0);
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);

    const load = useCallback(async () => {
        const data = await api.getUsers(search || undefined, page, 25);
        setUsers(data.users || []); setTotal(data.total || 0);
    }, [search, page]);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Users" subtitle={`${total} registered users`}>
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>
            <div className="glass-card p-5">
                <div className="mb-5 max-w-sm">
                    <SearchBar value={search} onChange={setSearch} placeholder="Search by email..." onSubmit={load} />
                </div>
                <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                        <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                            <th className="text-left py-3 px-4 font-medium">User</th>
                            <th className="text-left py-3 px-4 font-medium">Role</th>
                            <th className="text-left py-3 px-4 font-medium">Tier</th>
                            <th className="text-left py-3 px-4 font-medium">Joined</th>
                            <th className="text-right py-3 px-4 font-medium">Actions</th>
                        </tr></thead>
                        <tbody>
                            {users.map((u: any) => (
                                <tr key={u.id} className="table-row">
                                    <td className="py-3 px-4">
                                        <div className="flex items-center gap-3">
                                            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-accent/20 to-aura-purple/20 flex items-center justify-center text-xs font-bold text-white/70">
                                                {(u.email || '?')[0].toUpperCase()}
                                            </div>
                                            <span className="text-white/80">{u.email}</span>
                                        </div>
                                    </td>
                                    <td className="py-3 px-4 text-white/50">{u.role}</td>
                                    <td className="py-3 px-4"><TierBadge tier={u.tier || 'free'} /></td>
                                    <td className="py-3 px-4 text-white/40">{new Date(u.createdAt).toLocaleDateString()}</td>
                                    <td className="py-3 px-4">
                                        <div className="flex items-center justify-end gap-2">
                                            {u.role !== 'admin' && u.tier !== 'free' && (
                                                <button onClick={async () => { await api.revokeSubscription(u.id); load(); }}
                                                    className="p-1.5 rounded-lg hover:bg-aura-amber/10 text-white/30 hover:text-aura-amber transition-colors" title="Revoke">
                                                    <Ban className="w-4 h-4" />
                                                </button>
                                            )}
                                            {u.role !== 'admin' && (
                                                <button onClick={async () => { if(confirm(`Delete ${u.email}?`)) { await api.deleteUser(u.id); load(); }}}
                                                    className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors" title="Delete">
                                                    <Trash2 className="w-4 h-4" />
                                                </button>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    {users.length === 0 && <EmptyState icon={Users} title="No users found" />}
                </div>
                <Pagination page={page} pages={Math.ceil(total / 25)} onChange={setPage} />
            </div>
        </div>
    );
}
