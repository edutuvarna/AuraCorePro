'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { Crown } from 'lucide-react';

export function RedeemInvitationPage() {
  const [token, setToken] = useState('');
  const [email, setEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    const hash = window.location.hash; // #/invite?token=...&email=...
    const qsStart = hash.indexOf('?');
    if (qsStart > -1) {
      const params = new URLSearchParams(hash.slice(qsStart + 1));
      setToken(params.get('token') ?? '');
      setEmail(params.get('email') ?? '');
    }
  }, []);

  const submit = async () => {
    setError('');
    if (newPassword.length < 10) return setError('Password must be at least 10 characters');
    if (newPassword !== confirm) return setError('Passwords do not match');
    setLoading(true);
    const r = await api.redeemInvitation(token, email, newPassword);
    setLoading(false);
    if (r.ok && r.data?.accessToken) {
      localStorage.setItem('aura_token', r.data.accessToken);
      setDone(true);
      setTimeout(() => window.location.assign('/'), 1500);
    } else setError(r.data?.error ?? 'Invitation invalid or expired');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><div className="text-center"><Crown className="w-10 h-10 text-accent mx-auto" /><p className="text-lg mt-2">Welcome! Redirecting…</p></div></div>;

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="w-full max-w-md glass-card p-8 space-y-4">
        <h2 className="text-xl font-display font-bold">Welcome! Set your password.</h2>
        <p className="text-sm text-white/60">Account: <code>{email}</code></p>
        <input type="password" placeholder="New password (min 10 chars)" value={newPassword} onChange={e => setNewPassword(e.target.value)} className="input-dark w-full" />
        <input type="password" placeholder="Confirm password" value={confirm} onChange={e => setConfirm(e.target.value)} className="input-dark w-full" />
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <button onClick={submit} disabled={loading || !token} className="btn-primary w-full">{loading ? 'Redeeming…' : 'Activate Account'}</button>
      </div>
    </div>
  );
}
