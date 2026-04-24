'use client';

import { useEffect, useState } from 'react';
import { FileText, Download, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';

export function AdminActionLogPage() {
  const [items, setItems] = useState<any[]>([]);
  const [stats, setStats] = useState<any>(null);
  const [filters, setFilters] = useState<{ actorEmail?: string; action?: string; dateFrom?: string; dateTo?: string }>({});
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const [list, s] = await Promise.all([api.listAdminActionLog(filters), api.getAdminActionStats()]);
    setItems(list.items ?? []); setStats(s); setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><FileText className="w-6 h-6" />Admin Action Log</h1>
        <a href={api.exportAdminActionLogCsvUrl(filters)} className="btn-primary inline-flex items-center gap-2" download>
          <Download className="w-4 h-4" />Export CSV
        </a>
      </div>
      {stats && (
        <div className="grid grid-cols-3 gap-3">
          <div className="glass-card p-3"><div className="text-xs text-white/50">Total</div><div className="text-2xl font-bold">{stats.total}</div></div>
          <div className="glass-card p-3"><div className="text-xs text-white/50">Last 24h</div><div className="text-2xl font-bold">{stats.last24h}</div></div>
          <div className="glass-card p-3"><div className="text-xs text-white/50">Last 7d</div><div className="text-2xl font-bold">{stats.last7d}</div></div>
        </div>
      )}
      <div className="glass-card p-3 flex gap-2">
        <input placeholder="Actor email" value={filters.actorEmail ?? ''} onChange={e => setFilters({ ...filters, actorEmail: e.target.value })} className="input-dark flex-1"/>
        <input placeholder="Action" value={filters.action ?? ''} onChange={e => setFilters({ ...filters, action: e.target.value })} className="input-dark flex-1"/>
        <input type="date" value={filters.dateFrom ?? ''} onChange={e => setFilters({ ...filters, dateFrom: e.target.value })} className="input-dark"/>
        <input type="date" value={filters.dateTo ?? ''} onChange={e => setFilters({ ...filters, dateTo: e.target.value })} className="input-dark"/>
        <button onClick={refresh} className="btn-primary"><RefreshCw className="w-4 h-4" /></button>
      </div>
      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Actor</th><th className="p-3 text-left">Action</th>
              <th className="p-3 text-left">Target</th><th className="p-3 text-left">IP</th><th className="p-3 text-left">When</th>
            </tr></thead>
            <tbody>
              {items.map((r, i) => (
                <tr key={i} className="border-t border-white/5">
                  <td className="p-3">{r.actorEmail}</td><td className="p-3"><code className="text-xs">{r.action}</code></td>
                  <td className="p-3 text-white/60">{r.targetType} {r.targetId ? `#${r.targetId.slice(0,8)}` : ''}</td>
                  <td className="p-3 text-white/40">{r.ipAddress}</td>
                  <td className="p-3 text-white/50">{new Date(r.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
