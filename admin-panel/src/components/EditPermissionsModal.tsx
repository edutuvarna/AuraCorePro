'use client';

import { useState } from 'react';
import { X, Shield } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';
import type { AdminAccount } from '@/lib/types';

export function EditPermissionsModal({ admin, onClose, onSaved }: {
  admin: AdminAccount;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const save = async () => {
    setError(''); setLoading(true);
    const res = await api.applyAdminTemplate(admin.id, {
      template,
      customKeys: template === 'Custom' ? customKeys : undefined,
    });
    setLoading(false);
    if (res.ok) { onSaved(); onClose(); }
    else setError(res.data?.error ?? 'Failed');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold flex items-center gap-2"><Shield className="w-5 h-5" />Edit permissions</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <p className="text-sm text-white/60">Account: <code className="text-xs">{admin.email}</code></p>
        <div className="glass-card p-3 bg-aura-red/5 border border-aura-red/20 text-xs text-aura-red">
          This replaces the admin&apos;s entire grant set. Existing grants are revoked (preserved for audit trail). Applies immediately.
        </div>
        <div>
          <label className="text-xs text-white/50 block mb-1">New template</label>
          <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
            <option value="Default">Default — no Tier 2 actions</option>
            <option value="Trusted">Trusted — all Tier 2 actions</option>
            <option value="ReadOnly">Read-Only — block all destructive actions</option>
            <option value="Custom">Custom — pick specific permissions</option>
          </select>
        </div>
        {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={save} disabled={loading} className="btn-primary">{loading ? 'Applying…' : 'Apply'}</button>
        </div>
      </div>
    </div>
  );
}
