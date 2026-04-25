'use client';

import { useEffect, useState } from 'react';
import { Check, X, RefreshCw, Users2 } from 'lucide-react';
import { api } from '@/lib/api';
import { on, off } from '@/lib/signalr';
import { Combobox } from '@/components/Combobox';
import type { PermissionRequest } from '@/lib/types';

export function PermissionRequestsPage() {
  const [items, setItems] = useState<PermissionRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<'pending'|'approved'|'denied'|'cancelled'>('pending');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [note, setNote] = useState('');

  const refresh = async () => {
    setLoading(true);
    const r = await api.listPermissionRequests(status);
    setItems(r.items ?? []);
    setLoading(false);
  };

  useEffect(() => { refresh(); }, [status]);

  useEffect(() => {
    const handler = (ev: any) => {
      if (status !== 'pending') return;
      setItems(prev => [{ id: ev.requestId, permissionKey: ev.permissionKey, reason: ev.reason, status: 'pending', requestedAt: ev.requestedAt, adminEmail: ev.adminEmail }, ...prev]);
    };
    on('PermissionRequested', handler);
    return () => off('PermissionRequested', handler);
  }, [status]);

  const approve = async (id: string) => {
    const { ok } = await api.approvePermissionRequest(id, null, note || undefined);
    if (ok) refresh();
  };
  const deny = async (id: string) => {
    const { ok } = await api.denyPermissionRequest(id, note || undefined);
    if (ok) refresh();
  };
  const bulkApprove = async () => {
    if (selected.size === 0) return;
    await api.bulkApprovePermissionRequests(Array.from(selected));
    setSelected(new Set());
    refresh();
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Users2 className="w-6 h-6"/>Permission Requests</h1>
        <Combobox
          value={status}
          onChange={v => setStatus(v as any)}
          options={[
            { value: 'pending', label: 'Pending' },
            { value: 'approved', label: 'Approved' },
            { value: 'denied', label: 'Denied' },
            { value: 'cancelled', label: 'Cancelled' },
          ]}
        />
      </div>

      {selected.size > 0 && (
        <div className="glass-card p-3 flex items-center gap-3">
          <span className="text-sm text-white/70">{selected.size} selected</span>
          <button onClick={bulkApprove} className="btn-primary-sm">Approve Selected</button>
        </div>
      )}

      <div className="glass-card overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-white/50"><RefreshCw className="w-6 h-6 inline animate-spin" /></div>
        ) : items.length === 0 ? (
          <div className="p-8 text-center text-white/50">No requests.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="p-3 text-left">
                  <input type="checkbox"
                    checked={selected.size === items.length && items.length > 0}
                    onChange={e => setSelected(e.target.checked ? new Set(items.map(i => i.id)) : new Set())} />
                </th>
                <th className="p-3 text-left">Admin</th>
                <th className="p-3 text-left">Permission</th>
                <th className="p-3 text-left">Reason</th>
                <th className="p-3 text-left">Requested</th>
                <th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(r => (
                <tr key={r.id} className="border-t border-white/5">
                  <td className="p-3">
                    {r.status === 'pending' && (
                      <input type="checkbox" checked={selected.has(r.id)}
                        onChange={e => {
                          setSelected(prev => {
                            const next = new Set(prev);
                            e.target.checked ? next.add(r.id) : next.delete(r.id);
                            return next;
                          });
                        }} />
                    )}
                  </td>
                  <td className="p-3">{r.adminEmail}</td>
                  <td className="p-3"><code className="text-xs">{r.permissionKey}</code></td>
                  <td className="p-3 max-w-sm truncate" title={r.reason}>{r.reason}</td>
                  <td className="p-3 text-white/50">{new Date(r.requestedAt).toLocaleString()}</td>
                  <td className="p-3 text-right space-x-2">
                    {r.status === 'pending' ? (
                      <>
                        <button onClick={() => approve(r.id)} className="btn-primary-sm inline-flex items-center gap-1"><Check className="w-3 h-3"/>Approve</button>
                        <button onClick={() => deny(r.id)}   className="btn-danger-sm inline-flex items-center gap-1"><X className="w-3 h-3"/>Deny</button>
                      </>
                    ) : (
                      <span className="text-xs text-white/50">{r.status}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div>
        <label className="block text-xs text-white/40 mb-1">Review note (optional, applied to next approve/deny action)</label>
        <input value={note} onChange={e => setNote(e.target.value)} className="input-dark w-full" placeholder="e.g. 'Approved for customer escalation INC-3421'" />
      </div>
    </div>
  );
}
