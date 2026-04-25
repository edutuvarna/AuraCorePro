/**
 * Updates page — manage app version releases (publish form + table of
 * existing releases with delete action). Phase 6.6 release-pipeline
 * territory; the inline form maps to api.publishUpdate, which uploads to
 * the R2 download bucket and mirrors to GitHub Releases.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-7 + 9-10 follow the same
 * convention).
 *
 * StatusBadge + EmptyState lifted in W2.T11 to shared `@/components/`.
 * Wave 3 / Task 17: inline `<table>` swapped for `<DataTable>` (responsive
 * card list below 768px). Per-row Trash button gets `btn-action`
 * (T1.6 — 44px tap target on mobile). Browser `confirm()` for delete kept
 * 1:1 — out of scope for the cosmetic sweep (Wave 5 polish can wire
 * `ConfirmDialog`).
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx; W2.T11 — primitives lifted.
 */

'use client';

import { useState, useEffect } from 'react';
import { Plus, Send, Trash2, Zap } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { Combobox } from '@/components/Combobox';
import { useRole } from '@/lib/roleContext';
import { usePermissions } from '@/hooks/usePermissions';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';

export function UpdatesPage() {
    const role = useRole();
    const { has, hasPending } = usePermissions(role);
    const [reqOpen, setReqOpen] = useState(false);
    const [updates, setUpdates] = useState<any[]>([]);
    const [form, setForm] = useState({ version: '', downloadUrl: '', releaseNotes: '', channel: 'stable', isMandatory: false });
    const [showForm, setShowForm] = useState(false);
    const [msg, setMsg] = useState('');

    useEffect(() => { api.getUpdates().then(setUpdates); }, []);

    if (role === 'admin' && !has('tab:updates')) {
        return (
            <>
                <LockedTabPlaceholder
                    tabName="Updates"
                    permissionKey="tab:updates"
                    hasPending={hasPending('tab:updates')}
                    onRequestStart={() => setReqOpen(true)}
                />
                {reqOpen && (
                    <PermissionRequestDialog
                        isOpen permissionKey="tab:updates"
                        onClose={() => setReqOpen(false)}
                        onSubmit={async (k, r) => (await api.createPermissionRequest(k, r)).ok}
                    />
                )}
            </>
        );
    }

    const publish = async () => {
        const { ok, data } = await api.publishUpdate(form);
        if (ok) { setMsg('Update published!'); setShowForm(false); api.getUpdates().then(setUpdates); setForm({ version: '', downloadUrl: '', releaseNotes: '', channel: 'stable', isMandatory: false }); }
        else setMsg(data?.error || 'Failed');
    };

    return (
        <div className="animate-fade-in">
            <PageHeader title="Updates" subtitle="Manage app update releases">
                <button onClick={() => setShowForm(!showForm)} className="btn-primary flex items-center gap-2"><Plus className="w-4 h-4" />Publish Update</button>
            </PageHeader>

            {showForm && (
                <div className="glass-card p-6 mb-5 animate-slide-up max-w-2xl">
                    <h3 className="font-display font-semibold mb-4">New Update</h3>
                    <div className="grid grid-cols-2 gap-4 mb-4">
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Version</label>
                            <input value={form.version} onChange={e => setForm({ ...form, version: e.target.value })} className="input-dark w-full" placeholder="1.6.0" />
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Channel</label>
                            <Combobox
                                value={form.channel}
                                onChange={v => setForm({ ...form, channel: v })}
                                options={[
                                    { value: 'stable', label: 'Stable' },
                                    { value: 'beta', label: 'Beta' },
                                ]}
                                className="w-full"
                            />
                        </div>
                    </div>
                    <div className="mb-4">
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Download URL</label>
                        <input value={form.downloadUrl} onChange={e => setForm({ ...form, downloadUrl: e.target.value })} className="input-dark w-full" placeholder="https://..." />
                    </div>
                    <div className="mb-4">
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Release Notes</label>
                        <textarea value={form.releaseNotes} onChange={e => setForm({ ...form, releaseNotes: e.target.value })} className="input-dark w-full h-24 resize-none" />
                    </div>
                    <div className="flex items-center justify-between">
                        <label className="flex items-center gap-2 cursor-pointer">
                            <input type="checkbox" checked={form.isMandatory} onChange={e => setForm({ ...form, isMandatory: e.target.checked })} className="rounded" />
                            <span className="text-sm text-white/60">Mandatory update</span>
                        </label>
                        <div className="flex gap-3">
                            <button onClick={() => setShowForm(false)} className="btn-ghost">Cancel</button>
                            <button onClick={publish} className="btn-primary flex items-center gap-2"><Send className="w-4 h-4" />Publish</button>
                        </div>
                    </div>
                    {msg && <p className={`text-sm mt-3 ${msg.includes('!') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
                </div>
            )}

            <div className="glass-card p-5">
                <DataTable<any>
                    columns={[
                        {
                            key: 'version',
                            header: 'Version',
                            isCardTitle: true,
                            render: (u) => <span className="font-mono font-semibold text-accent">{u.version}</span>,
                        },
                        {
                            key: 'channel',
                            header: 'Channel',
                            render: (u) => <StatusBadge status={u.channel || 'stable'} />,
                        },
                        {
                            key: 'mandatory',
                            header: 'Mandatory',
                            render: (u) => u.isMandatory
                                ? <span className="text-aura-amber">Yes</span>
                                : <span className="text-white/30">No</span>,
                        },
                        {
                            key: 'published',
                            header: 'Published',
                            render: (u) => <span className="text-white/40">{new Date(u.createdAt).toLocaleDateString()}</span>,
                        },
                        {
                            key: 'actions',
                            header: 'Actions',
                            cellClassName: 'text-right',
                            render: (u) => (
                                <div className="flex justify-end">
                                    <button onClick={async () => { if(confirm('Delete?')) { await api.deleteUpdate(u.id); api.getUpdates().then(setUpdates); }}}
                                        className="btn-action p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors inline-flex items-center justify-center"><Trash2 className="w-4 h-4" /></button>
                                </div>
                            ),
                        },
                    ] as DataTableColumn<any>[]}
                    rows={updates}
                    rowKey={(u) => u.id}
                    emptyState={<EmptyState icon={Zap} title="No updates published" subtitle="Click 'Publish Update' to create one" />}
                />
            </div>
        </div>
    );
}
