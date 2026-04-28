'use client';

import { useState } from 'react';
import { X } from 'lucide-react';
import { api } from '@/lib/api';
import { Combobox } from './Combobox';

interface AdminAccount {
  id: string;
  email: string;
  role: string;
}

type Mode = 'promote' | 'demote';

interface Props {
  mode: Mode;
  selected: AdminAccount[];
  onClose: () => void;
  onSuccess: () => void;
}

const TEMPLATE_OPTIONS = [
  { value: 'Default', label: 'Default (no Tier 2 permissions)' },
  { value: 'Trusted', label: 'Trusted (all Tier 2 permissions)' },
  { value: 'ReadOnly', label: 'ReadOnly (read-only flag, no permissions)' },
];

const FORCE_PW_OPTIONS = [
  { value: 'on_first_login', label: 'On first login' },
  { value: 'within_7_days', label: 'Within 7 days' },
  { value: 'within_30_days', label: 'Within 30 days' },
  { value: 'never', label: 'Never' },
];

export function BulkRoleChangeModal({ mode, selected, onClose, onSuccess }: Props) {
  const [template, setTemplate] = useState('Default');
  const [forcePw, setForcePw] = useState('on_first_login');
  const [require2fa, setRequire2fa] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const apply = async () => {
    setSubmitting(true);
    setError('');
    try {
      const ids = selected.map((s) => s.id);
      const res = mode === 'promote'
        ? await api.bulkPromoteUsersToAdmin(ids, template, forcePw, require2fa)
        : await api.bulkDemoteAdminsToUser(ids);
      if (!res.ok) {
        setError(res.data?.error ?? 'Bulk operation failed');
        return;
      }
      onSuccess();
      onClose();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold">
            Bulk {mode === 'promote' ? 'Promote' : 'Demote'} — {selected.length} selected
          </h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>

        {mode === 'promote' && (
          <>
            <div>
              <label className="text-xs text-white/50 block mb-1">Template</label>
              <Combobox value={template} onChange={setTemplate} options={TEMPLATE_OPTIONS} className="w-full" />
            </div>
            <div>
              <label className="text-xs text-white/50 block mb-1">Force password change</label>
              <Combobox value={forcePw} onChange={setForcePw} options={FORCE_PW_OPTIONS} className="w-full" />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={require2fa} onChange={(e) => setRequire2fa(e.target.checked)} />
              Require 2FA
            </label>
          </>
        )}

        <div className="bg-white/5 rounded p-3 space-y-1 max-h-48 overflow-y-auto">
          <div className="text-xs text-white/50 mb-2">Audit preview ({selected.length} accounts):</div>
          {selected.map((s) => (
            <div key={s.id} className="text-xs font-mono">
              {s.email}: {s.role} → {mode === 'promote' ? `admin (${template}${require2fa ? ', 2FA required' : ''})` : 'user (grants revoked)'}
            </div>
          ))}
        </div>

        {error && <div className="text-xs text-aura-red">{error}</div>}

        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost" disabled={submitting}>Cancel</button>
          <button onClick={apply} className="btn-primary" disabled={submitting}>
            {submitting ? 'Applying…' : `Apply to ${selected.length}`}
          </button>
        </div>
      </div>
    </div>
  );
}
