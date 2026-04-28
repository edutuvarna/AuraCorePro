'use client';

import { useEffect, useState } from 'react';
import { Clock, Play } from 'lucide-react';
import { api } from '@/lib/api';

interface RetentionPolicy {
  retentionDays: number;
  lastRunAt: string | null;
  lastRunDeletedRows: number;
  totalRows: number;
  oldestAt: string | null;
}

export function AuditRetentionPage() {
  const [policy, setPolicy] = useState<RetentionPolicy | null>(null);
  const [pendingDays, setPendingDays] = useState(0);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [message, setMessage] = useState('');

  const refresh = async () => {
    const p = await api.getAuditRetentionPolicy();
    if (p) {
      setPolicy(p);
      setPendingDays(p.retentionDays);
    }
  };
  useEffect(() => { refresh(); }, []);

  const save = async () => {
    setSaving(true); setMessage('');
    try {
      const res = await api.setAuditRetentionPolicy(pendingDays);
      setMessage(res.ok ? `Saved: ${pendingDays} days` : (res.data?.error ?? 'Save failed'));
      if (res.ok) refresh();
    } finally {
      setSaving(false);
    }
  };

  const runNow = async () => {
    setRunning(true); setMessage('');
    try {
      const res = await api.runAuditRetentionNow();
      setMessage(res.ok ? `Cleanup complete: ${res.data.deleted} rows deleted` : (res.data?.error ?? 'Run failed'));
      if (res.ok) refresh();
    } finally {
      setRunning(false);
    }
  };

  if (!policy) return <div className="p-8 text-center text-white/50">Loading…</div>;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2">
        <Clock className="w-6 h-6" />Audit Log Retention
      </h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Total rows</div>
          <div className="text-2xl font-bold">{policy.totalRows.toLocaleString()}</div>
        </div>
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Oldest entry</div>
          <div className="text-sm">{policy.oldestAt ? new Date(policy.oldestAt).toLocaleString() : '—'}</div>
        </div>
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Last cleanup</div>
          <div className="text-sm">{policy.lastRunAt ? new Date(policy.lastRunAt).toLocaleString() : 'Never'}</div>
          <div className="text-xs text-white/50 mt-1">{policy.lastRunDeletedRows.toLocaleString()} rows deleted</div>
        </div>
      </div>

      <div className="glass-card p-6 space-y-4">
        <div>
          <h3 className="text-lg font-bold mb-2">Retention policy</h3>
          <label className="block text-xs text-white/50">Retain audit logs for (days)
            <input
              type="number"
              min={30}
              max={3650}
              value={pendingDays}
              onChange={(e) => setPendingDays(Number(e.target.value))}
              className="input-dark w-full mt-1"
            />
          </label>
          <div className="text-xs text-white/40 mt-1">Range: 30–3650 days. Daily cleanup runs at the configured retention window.</div>
        </div>

        <div className="flex gap-2">
          <button onClick={save} className="btn-primary" disabled={saving || pendingDays === policy.retentionDays}>
            {saving ? 'Saving…' : 'Save'}
          </button>
          <button onClick={runNow} className="btn-ghost flex items-center gap-1" disabled={running}>
            <Play className="w-4 h-4" />{running ? 'Running…' : 'Run cleanup now'}
          </button>
        </div>

        {message && <div className="text-sm text-cyan-300">{message}</div>}
      </div>
    </div>
  );
}
