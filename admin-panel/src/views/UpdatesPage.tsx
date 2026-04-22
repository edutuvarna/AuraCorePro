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
 * StatusBadge / EmptyState are temporarily duplicated from page.tsx
 * (Strategy B). Task 11 lifts them into shared `@/components/` and call sites
 * flip to the import. DataTable conversion is Wave 3 Task 16 — keep the inline
 * `<table>` 1:1 with the monolith for now.
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx.
 */

'use client';

import { useState, useEffect } from 'react';
import { Plus, Send, Trash2, Zap } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

// Temporarily inlined — Task 11 will lift to @/components/StatusBadge
function StatusBadge({ status }: { status: string }) {
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

// Temporarily inlined — Task 11 will lift to @/components/EmptyState
function EmptyState({ icon: Icon, title, subtitle }: { icon: any; title: string; subtitle?: string }) {
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

export function UpdatesPage() {
    const [updates, setUpdates] = useState<any[]>([]);
    const [form, setForm] = useState({ version: '', downloadUrl: '', releaseNotes: '', channel: 'stable', isMandatory: false });
    const [showForm, setShowForm] = useState(false);
    const [msg, setMsg] = useState('');

    useEffect(() => { api.getUpdates().then(setUpdates); }, []);

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
                            <select value={form.channel} onChange={e => setForm({ ...form, channel: e.target.value })} className="input-dark w-full">
                                <option value="stable">Stable</option><option value="beta">Beta</option>
                            </select>
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
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Version</th>
                        <th className="text-left py-3 px-4 font-medium">Channel</th>
                        <th className="text-left py-3 px-4 font-medium">Mandatory</th>
                        <th className="text-left py-3 px-4 font-medium">Published</th>
                        <th className="text-right py-3 px-4 font-medium">Actions</th>
                    </tr></thead>
                    <tbody>
                        {updates.map((u: any) => (
                            <tr key={u.id} className="table-row">
                                <td className="py-3 px-4 font-mono font-semibold text-accent">{u.version}</td>
                                <td className="py-3 px-4"><StatusBadge status={u.channel || 'stable'} /></td>
                                <td className="py-3 px-4">{u.isMandatory ? <span className="text-aura-amber">Yes</span> : <span className="text-white/30">No</span>}</td>
                                <td className="py-3 px-4 text-white/40">{new Date(u.createdAt).toLocaleDateString()}</td>
                                <td className="py-3 px-4 text-right">
                                    <button onClick={async () => { if(confirm('Delete?')) { await api.deleteUpdate(u.id); api.getUpdates().then(setUpdates); }}}
                                        className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {updates.length === 0 && <EmptyState icon={Zap} title="No updates published" subtitle="Click 'Publish Update' to create one" />}
            </div>
        </div>
    );
}
