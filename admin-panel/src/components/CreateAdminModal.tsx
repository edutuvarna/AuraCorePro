'use client';

import { useState } from 'react';
import { X, UserPlus } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';

export function CreateAdminModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [email, setEmail] = useState('');
  const [sendInvitation, setSendInvitation] = useState(true);
  const [initialPassword, setInitialPassword] = useState('');
  const [forcePasswordChange, setForce] = useState<'on_first_login'|'within_7_days'|'within_30_days'|'never'>('on_first_login');
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [require2fa, setRequire2fa] = useState(true);
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const generatePassword = () => {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*';
    let p = '';
    for (let i = 0; i < 16; i++) p += chars[Math.floor(Math.random() * chars.length)];
    setInitialPassword(p);
  };

  const submit = async () => {
    setError(''); setLoading(true);
    const res = await api.createAdminAccount({
      email, sendInvitation,
      initialPassword: sendInvitation ? undefined : initialPassword,
      forcePasswordChange, template, require2fa,
      customKeys: template === 'Custom' ? customKeys.map(k => ({ permissionKey: k.permissionKey, expiresAt: k.expiresAt })) : undefined,
    });
    setLoading(false);
    if (res.ok) { onCreated(); onClose(); }
    else setError(res.data?.error ?? 'Failed to create admin');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold flex items-center gap-2"><UserPlus className="w-5 h-5" />New admin account</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <input value={email} onChange={e => setEmail(e.target.value)} className="input-dark w-full" placeholder="admin@company.com" type="email" />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={sendInvitation} onChange={e => setSendInvitation(e.target.checked)} />
          Send invitation email (admin picks own password)
        </label>
        {!sendInvitation && (
          <div className="flex gap-2">
            <input value={initialPassword} onChange={e => setInitialPassword(e.target.value)} className="input-dark flex-1" placeholder="Initial password (min 10 chars)" />
            <button onClick={generatePassword} className="btn-ghost">Generate</button>
          </div>
        )}
        <div>
          <label className="text-xs text-white/50 block mb-1">Force password change</label>
          <select value={forcePasswordChange} onChange={e => setForce(e.target.value as 'on_first_login'|'within_7_days'|'within_30_days'|'never')} className="input-dark w-full">
            <option value="on_first_login">On first login</option>
            <option value="within_7_days">Within 7 days</option>
            <option value="within_30_days">Within 30 days</option>
            <option value="never">Never</option>
          </select>
        </div>
        <div>
          <label className="text-xs text-white/50 block mb-1">Permission template</label>
          <select value={template} onChange={e => setTemplate(e.target.value as 'Default'|'Trusted'|'ReadOnly'|'Custom')} className="input-dark w-full">
            <option value="Default">Default — no Tier 2 actions</option>
            <option value="Trusted">Trusted — all Tier 2 actions</option>
            <option value="ReadOnly">Read-Only — no destructive actions</option>
            <option value="Custom">Custom — per-permission config</option>
          </select>
        </div>
        {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={require2fa} onChange={e => setRequire2fa(e.target.checked)} />
          Require 2FA on this account
        </label>
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={submit} disabled={loading || !email} className="btn-primary">
            {loading ? 'Creating…' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}
