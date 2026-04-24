'use client';

import { useState } from 'react';
import { X, Send, RefreshCw } from 'lucide-react';
import { PERMISSION_LABELS, PermissionKey } from '@/lib/permissions';

export interface PermissionRequestDialogProps {
  isOpen: boolean;
  permissionKey: string;
  onClose: () => void;
  onSubmit: (key: string, reason: string) => Promise<boolean>;
}

export function PermissionRequestDialog({ isOpen, permissionKey, onClose, onSubmit }: PermissionRequestDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  if (!isOpen) return null;

  const label = PERMISSION_LABELS[permissionKey as PermissionKey] ?? permissionKey;

  const submit = async () => {
    const trimmed = reason.trim();
    if (trimmed.length < 50) { setError('Reason must be at least 50 characters so the superadmin has context.'); return; }
    if (trimmed.length > 500) { setError('Reason must be 500 characters or fewer.'); return; }
    setLoading(true); setError('');
    const ok = await onSubmit(permissionKey, trimmed);
    setLoading(false);
    if (ok) { setReason(''); onClose(); }
    else setError('Failed to send request. A pending request may already exist.');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4" role="dialog">
      <div className="glass-card w-full max-w-md p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold">Request access to {label}</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <div>
          <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">
            Why do you need this permission?
          </label>
          <textarea
            value={reason} onChange={e => setReason(e.target.value)}
            maxLength={500} rows={5}
            placeholder="Explain the specific use case, customer ticket, or incident that requires this access."
            className="input-dark w-full resize-none"
          />
          <div className="flex items-center justify-between mt-1 text-xs text-white/40">
            <span>50 min · 500 max</span>
            <span>{reason.length} / 500</span>
          </div>
        </div>
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex items-center gap-2 justify-end pt-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={submit} disabled={loading} className="btn-primary inline-flex items-center gap-2">
            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
            {loading ? 'Submitting...' : 'Submit Request'}
          </button>
        </div>
      </div>
    </div>
  );
}
