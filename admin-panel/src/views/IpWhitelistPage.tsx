/**
 * IP Whitelist page — trusted IP management for the login rate limiter.
 * Whitelisted IPs skip the per-IP login throttle (3 fails/30min); admin
 * access stays open to all IPs with valid creds + 2FA. Phase 6.9 hotfix
 * (8f9c0b6) added the explanation banner copy preserved here verbatim.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-9 follow the same
 * convention).
 *
 * NOTE on ConfirmDialog: the Phase 6.9 hotfix history mentions ConfirmDialog
 * being wired for delete actions, but the current monolith body uses a direct
 * `await api.removeWhitelistIp(ip.ip)` with no modal. This extraction is a
 * 1:1 lift — no ConfirmDialog import is added. Wave 5 polish (Task 22+) can
 * decide whether to wire `@/components/ConfirmDialog` here.
 *
 * EmptyState lifted in W2.T11 to shared `@/components/`.
 * Wave 3 / Task 17: inline `<table>` swapped for `<DataTable>` (responsive
 * card list below 768px). Per-row Trash button gets `btn-action`
 * (T1.6 — 44px tap target on mobile).
 *
 * Phase 6.10 W2.T10 — extracted from page.tsx (originally `WhitelistPage`,
 * renamed `IpWhitelistPage` per task plan for clarity); W2.T11 — EmptyState lifted.
 */

'use client';

import { useState, useEffect } from 'react';
import { Shield, Globe, Plus, Trash2 } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { useRole } from '@/lib/roleContext';
import { usePermissions } from '@/hooks/usePermissions';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';

export function IpWhitelistPage() {
    const role = useRole();
    const { has, hasPending } = usePermissions(role);
    const [reqOpen, setReqOpen] = useState(false);
    const [ips, setIps] = useState<any[]>([]);
    const [newIp, setNewIp] = useState('');
    const [newLabel, setNewLabel] = useState('');
    const [msg, setMsg] = useState('');
    const [myIp, setMyIp] = useState('');

    const load = async () => {
        const [w, ip] = await Promise.all([api.getWhitelist(), api.getMyIp()]);
        setIps(w || []); if (ip?.ip) setMyIp(ip.ip);
    };

    useEffect(() => { load(); }, []);

    if (role === 'admin' && !has('tab:ipwhitelist')) {
        return (
            <>
                <LockedTabPlaceholder
                    tabName="IP Whitelist"
                    permissionKey="tab:ipwhitelist"
                    hasPending={hasPending('tab:ipwhitelist')}
                    onRequestStart={() => setReqOpen(true)}
                />
                {reqOpen && (
                    <PermissionRequestDialog
                        isOpen permissionKey="tab:ipwhitelist"
                        onClose={() => setReqOpen(false)}
                        onSubmit={async (k, r) => (await api.createPermissionRequest(k, r)).ok}
                    />
                )}
            </>
        );
    }

    const addIp = async () => {
        if (!newIp) return;
        const { ok, data } = await api.addWhitelistIp(newIp, newLabel || undefined);
        if (ok) { setNewIp(''); setNewLabel(''); setMsg(''); load(); }
        else setMsg(data?.error || 'Failed');
    };

    return (
        <div className="animate-fade-in">
            <PageHeader title="IP Whitelist" subtitle="Trusted IPs bypass the login rate limit (3 fails/30min per-IP, 5 fails/30min per-email)">
                <button onClick={async () => {
                    if (myIp) {
                        const { ok } = await api.addWhitelistIp(myIp, 'Auto-added');
                        if (ok) load();
                    }
                }} className="btn-primary flex items-center gap-2"><Globe className="w-4 h-4" />Whitelist My IP{myIp ? ` (${myIp})` : ''}</button>
            </PageHeader>

            {/* Explanation banner */}
            <div className="glass-card p-4 mb-5 text-xs text-white/60 leading-relaxed max-w-3xl">
                <strong className="text-white/80">How this works:</strong> whitelisted IPs{' '}
                <strong className="text-accent">skip the login rate limit</strong>. Admin access is NOT restricted — all IPs
                can still log in with valid credentials + 2FA. Use this for trusted operational IPs (e.g. office, office VPN) so
                a distributed brute-force attempt from the same network doesn&apos;t lock you out. Whitelisting your own IP is safe
                and recommended.
            </div>

            {/* Add Form */}
            <div className="glass-card p-5 mb-5 max-w-xl">
                <h3 className="font-display font-semibold text-sm mb-4">Add IP Address</h3>
                <div className="flex items-end gap-3">
                    <div className="flex-1">
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">IP Address</label>
                        <input value={newIp} onChange={e => setNewIp(e.target.value)} className="input-dark w-full" placeholder="1.2.3.4" />
                    </div>
                    <div className="flex-1">
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Label (optional)</label>
                        <input value={newLabel} onChange={e => setNewLabel(e.target.value)} className="input-dark w-full" placeholder="Office" />
                    </div>
                    <button onClick={addIp} className="btn-primary shrink-0"><Plus className="w-4 h-4" /></button>
                </div>
                {msg && <p className="text-sm text-aura-red mt-2">{msg}</p>}
            </div>

            {/* IP List */}
            <div className="glass-card p-5">
                <DataTable<any>
                    columns={[
                        {
                            key: 'ip',
                            header: 'IP Address',
                            isCardTitle: true,
                            render: (ip) => <span className="font-mono text-accent">{ip.ip}</span>,
                        },
                        {
                            key: 'label',
                            header: 'Label',
                            render: (ip) => <span className="text-white/50">{ip.label || '-'}</span>,
                        },
                        {
                            key: 'added',
                            header: 'Added',
                            render: (ip) => <span className="text-white/40">{ip.addedAt ? new Date(ip.addedAt).toLocaleDateString() : '-'}</span>,
                        },
                        {
                            key: 'actions',
                            header: 'Actions',
                            cellClassName: 'text-right',
                            render: (ip) => (
                                <div className="flex justify-end">
                                    <button onClick={async () => { await api.removeWhitelistIp(ip.ip); load(); }}
                                        className="btn-action p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors inline-flex items-center justify-center"><Trash2 className="w-4 h-4" /></button>
                                </div>
                            ),
                        },
                    ] as DataTableColumn<any>[]}
                    rows={ips}
                    rowKey={(ip) => ip.ip}
                    emptyState={<EmptyState icon={Shield} title="No trusted IPs yet" subtitle="Add trusted IPs (e.g. office, home, office VPN) to exempt them from login rate-limit lockouts" />}
                />
            </div>
        </div>
    );
}
