'use client';

import { useEffect, useState } from 'react';
import { Gauge } from 'lucide-react';
import { api } from '@/lib/api';

export function APIRateLimitsPage() {
  const [items, setItems] = useState<any[]>([]);
  const [editing, setEditing] = useState<{ endpoint: string; requests: number; windowSeconds: number } | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const r = await api.getRateLimitPolicies();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const save = async () => {
    if (!editing) return;
    await api.updateRateLimitPolicy(editing.endpoint, editing.requests, editing.windowSeconds);
    setEditing(null); refresh();
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Gauge className="w-6 h-6" />API Rate Limits</h1>
      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Endpoint</th>
              <th className="p-3 text-left">Requests</th>
              <th className="p-3 text-left">Window (s)</th>
              <th className="p-3 text-right">Action</th>
            </tr></thead>
            <tbody>
              {items.map(p => (
                <tr key={p.endpoint} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{p.endpoint}</code></td>
                  <td className="p-3">{p.requests}</td>
                  <td className="p-3">{p.windowSeconds}</td>
                  <td className="p-3 text-right">
                    <button onClick={() => setEditing(p)} className="btn-primary-sm">Edit</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="glass-card w-full max-w-sm p-6 space-y-3">
            <h3 className="text-lg font-display font-bold">Edit {editing.endpoint}</h3>
            <label className="block text-xs text-white/50">Requests
              <input type="number" value={editing.requests} onChange={e => setEditing({ ...editing, requests: +e.target.value })} className="input-dark w-full mt-1" />
            </label>
            <label className="block text-xs text-white/50">Window (seconds)
              <input type="number" value={editing.windowSeconds} onChange={e => setEditing({ ...editing, windowSeconds: +e.target.value })} className="input-dark w-full mt-1" />
            </label>
            <div className="flex justify-end gap-2">
              <button onClick={() => setEditing(null)} className="btn-ghost">Cancel</button>
              <button onClick={save} className="btn-primary">Apply</button>
            </div>
          </div>
        </div>
      )}
      <p className="text-xs text-white/40">Edits persist to system_settings['rate_limit_policies'] and invalidate the 5-min cache. Hot-reload of the ASP.NET Core RateLimiter pipeline is queued for Phase 6.15.</p>
    </div>
  );
}
