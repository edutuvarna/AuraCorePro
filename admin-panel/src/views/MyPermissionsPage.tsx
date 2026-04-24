'use client';

import { useEffect, useState } from 'react';
import { Shield } from 'lucide-react';
import { api } from '@/lib/api';
import type { MyPermissionsResponse } from '@/lib/types';

export function MyPermissionsPage() {
  const [data, setData] = useState<MyPermissionsResponse | null>(null);

  const refresh = async () => setData(await api.getMyPermissions());
  useEffect(() => { refresh(); }, []);

  const cancel = async (id: string) => { await api.cancelPermissionRequest(id); refresh(); };

  if (!data) return <div className="p-8 text-center text-white/50">Loading…</div>;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Shield className="w-6 h-6" />My Permissions</h1>
      <div className="glass-card p-4">
        You have access to <strong>{data.activeGrantsCount} of {data.totalRestricted}</strong> restricted permissions.
      </div>

      <section>
        <h2 className="font-semibold mb-2">Active grants</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Granted by</th>
              <th className="p-3 text-left">Granted at</th><th className="p-3 text-left">Expires</th>
            </tr></thead>
            <tbody>
              {data.grants.length === 0 ? (
                <tr><td colSpan={4} className="p-4 text-center text-white/40">No active grants.</td></tr>
              ) : data.grants.map(g => (
                <tr key={g.permissionKey} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{g.permissionKey}</code></td>
                  <td className="p-3">{g.grantedByEmail ?? '—'}</td>
                  <td className="p-3 text-white/50">{new Date(g.grantedAt).toLocaleDateString()}</td>
                  <td className="p-3 text-white/50">{g.expiresAt ? new Date(g.expiresAt).toLocaleString() : 'never'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="font-semibold mb-2">Pending requests</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Requested at</th>
              <th className="p-3 text-left">Reason</th><th className="p-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {data.pending.length === 0 ? (
                <tr><td colSpan={4} className="p-4 text-center text-white/40">None pending.</td></tr>
              ) : data.pending.map(p => (
                <tr key={p.id} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{p.permissionKey}</code></td>
                  <td className="p-3 text-white/50">{new Date(p.requestedAt).toLocaleString()}</td>
                  <td className="p-3 max-w-sm truncate" title={p.reason}>{p.reason}</td>
                  <td className="p-3 text-right">
                    <button onClick={() => cancel(p.id)} className="btn-ghost-sm">Cancel</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="font-semibold mb-2">Recent denials</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Note</th>
              <th className="p-3 text-left">When</th>
            </tr></thead>
            <tbody>
              {data.recentDenials.length === 0 ? (
                <tr><td colSpan={3} className="p-4 text-center text-white/40">No recent denials.</td></tr>
              ) : data.recentDenials.map((d, i) => (
                <tr key={i} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{d.permissionKey}</code></td>
                  <td className="p-3">{d.reviewNote || '—'}</td>
                  <td className="p-3 text-white/50">{new Date(d.reviewedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
