// admin-panel/src/views/ChangePasswordPage.tsx
'use client';

import { useState } from 'react';
import { Key } from 'lucide-react';
import { api } from '@/lib/api';

export function ChangePasswordPage() {
  const [current, setCurrent] = useState('');
  const [newPw, setNewPw] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);

  const submit = async () => {
    setError('');
    if (newPw.length < 10) return setError('Password must be at least 10 characters');
    if (newPw !== confirm) return setError('Passwords do not match');
    const r = await api.changePassword(current, newPw);
    if (r.ok) { setDone(true); setTimeout(() => window.location.assign('/'), 1500); }
    else setError(r.data?.error ?? 'Failed');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><p>Password changed. Redirecting…</p></div>;

  return (
    <div className="max-w-md mx-auto mt-16 glass-card p-8 space-y-4">
      <h2 className="text-xl font-display font-bold flex items-center gap-2"><Key className="w-5 h-5" />Change password</h2>
      <input type="password" placeholder="Current password" value={current} onChange={e => setCurrent(e.target.value)} className="input-dark w-full" />
      <input type="password" placeholder="New password (min 10 chars)" value={newPw} onChange={e => setNewPw(e.target.value)} className="input-dark w-full" />
      <input type="password" placeholder="Confirm new password" value={confirm} onChange={e => setConfirm(e.target.value)} className="input-dark w-full" />
      {error && <div className="text-xs text-aura-red">{error}</div>}
      <button onClick={submit} className="btn-primary w-full">Update password</button>
    </div>
  );
}
