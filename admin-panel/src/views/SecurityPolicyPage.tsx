// admin-panel/src/views/SecurityPolicyPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { Lock, Shield } from 'lucide-react';
import { api } from '@/lib/api';

export function SecurityPolicyPage() {
  const [globalOn, setGlobalOn] = useState(false);
  const [accounts, setAccounts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const p = await api.getSecurityPolicy();
    if (p) {
      setGlobalOn(p.require2faForAllAdmins);
      setAccounts(p.perAccountOverrides);
    }
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const toggleGlobal = async () => {
    const next = !globalOn;
    setGlobalOn(next);
    await api.updateSecurityPolicy(next);
  };
  const toggleAccount = async (userId: string, value: boolean) => {
    setAccounts(prev => prev.map(a => a.userId === userId ? { ...a, require2fa: value } : a));
    await api.setAdminRequire2fa(userId, value);
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Lock className="w-6 h-6" />Security Policy</h1>
      {loading ? <div className="glass-card p-8 text-center text-white/50">Loading…</div> : (
        <>
          <div className="glass-card p-4 flex items-center justify-between">
            <div>
              <div className="font-semibold">Require 2FA for all admin accounts</div>
              <div className="text-xs text-white/50">When on, every admin must have TOTP enabled to log in.</div>
            </div>
            <button onClick={toggleGlobal} className={globalOn ? 'btn-primary' : 'btn-ghost'}>{globalOn ? 'On' : 'Off'}</button>
          </div>
          <div className="glass-card overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-white/5"><tr>
                <th className="p-3 text-left">Email</th><th className="p-3 text-left">Role</th>
                <th className="p-3 text-left">Per-account require 2FA</th>
              </tr></thead>
              <tbody>
                {accounts.map(a => (
                  <tr key={a.userId} className="border-t border-white/5">
                    <td className="p-3">{a.email}</td>
                    <td className="p-3">{a.role}</td>
                    <td className="p-3">
                      {a.role === 'superadmin' ? <span className="text-xs text-white/40">required (always)</span> : (
                        <label className="flex items-center gap-2 text-sm">
                          <input type="checkbox" checked={a.require2fa} onChange={e => toggleAccount(a.userId, e.target.checked)} />
                          {a.require2fa ? 'required' : 'optional'}
                        </label>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
