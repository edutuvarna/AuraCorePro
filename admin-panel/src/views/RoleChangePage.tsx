'use client';

import { useState } from 'react';
import { ArrowRightLeft } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';

export function RoleChangePage() {
  const [mode, setMode] = useState<'promote'|'demote'>('promote');
  const [userId, setUserId] = useState('');
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [forcePwd, setForcePwd] = useState<'on_first_login'|'within_7_days'|'within_30_days'|'never'>('on_first_login');
  const [require2fa, setRequire2fa] = useState(true);
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [status, setStatus] = useState<string>('');

  const run = async () => {
    setStatus('');
    const ok = mode === 'promote'
      ? (await api.promoteUserToAdmin(userId, { template, forcePasswordChange: forcePwd, require2fa, customKeys: template === 'Custom' ? customKeys : undefined })).ok
      : (await api.demoteAdminToUser(userId)).ok;
    setStatus(ok ? 'Success.' : 'Failed — check user id + role.');
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><ArrowRightLeft className="w-6 h-6" />Role Change (single-user)</h1>
      <div className="glass-card p-4 space-y-3">
        <div className="flex gap-2">
          <button onClick={() => setMode('promote')} className={mode==='promote'?'btn-primary':'btn-ghost'}>Promote user → admin</button>
          <button onClick={() => setMode('demote')}  className={mode==='demote' ?'btn-primary':'btn-ghost'}>Demote admin → user</button>
        </div>
        <input value={userId} onChange={e => setUserId(e.target.value)} placeholder="User ID (UUID)" className="input-dark w-full" />
        {mode === 'promote' && (
          <>
            <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
              <option value="Default">Default</option>
              <option value="Trusted">Trusted</option>
              <option value="ReadOnly">Read-Only</option>
              <option value="Custom">Custom</option>
            </select>
            {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
            <select value={forcePwd} onChange={e => setForcePwd(e.target.value as any)} className="input-dark w-full">
              <option value="on_first_login">Force change on first login</option>
              <option value="within_7_days">Force change within 7 days</option>
              <option value="within_30_days">Force change within 30 days</option>
              <option value="never">Never</option>
            </select>
            <label className="flex gap-2 text-sm"><input type="checkbox" checked={require2fa} onChange={e => setRequire2fa(e.target.checked)} />Require 2FA</label>
          </>
        )}
        <button onClick={run} className="btn-primary w-full" disabled={!userId}>Apply</button>
        {status && <div className="text-xs text-white/60">{status}</div>}
      </div>
      <p className="text-xs text-white/40">Bulk operations + audit preview deferred to Phase 6.12.</p>
    </div>
  );
}
