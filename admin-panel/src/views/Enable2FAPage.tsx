// admin-panel/src/views/Enable2FAPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';

const API = process.env.NEXT_PUBLIC_API_URL || 'https://api.auracore.pro';

async function post(path: string, body?: any) {
  const res = await fetch(API + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${localStorage.getItem('aura_token')}` },
    body: body ? JSON.stringify(body) : undefined,
  });
  return { ok: res.ok, data: await res.json().catch(() => ({})) };
}

export function Enable2FAPage() {
  const [secret, setSecret] = useState<string | null>(null);
  const [uri, setUri] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);

  useEffect(() => { (async () => {
    const r = await post('/api/auth/enable-2fa/generate');
    if (r.ok) { setSecret(r.data.secret); setUri(r.data.uri); }
    else setError(r.data?.error ?? 'Could not generate secret');
  })(); }, []);

  const confirm = async () => {
    const r = await post('/api/auth/enable-2fa/confirm', { code });
    if (r.ok) {
      localStorage.setItem('aura_token', r.data.accessToken);
      setDone(true);
      setTimeout(() => window.location.assign('/'), 1200);
    } else setError(r.data?.error ?? 'Invalid code');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><p>2FA enabled. Redirecting…</p></div>;

  return (
    <div className="max-w-md mx-auto mt-16 glass-card p-8 space-y-4">
      <h2 className="text-xl font-display font-bold flex items-center gap-2"><ShieldCheck className="w-5 h-5" />Enable two-factor authentication</h2>
      {secret && uri ? (
        <>
          <p className="text-sm text-white/60">Scan this URI with Google Authenticator / 1Password / Authy, or enter the secret manually.</p>
          <div className="glass-card p-3 text-xs font-mono break-all">{uri}</div>
          <details className="text-xs">
            <summary className="cursor-pointer text-white/60">Manual secret (click to reveal)</summary>
            <div className="mt-2 font-mono text-sm">{secret}</div>
          </details>
          <input value={code} onChange={e => setCode(e.target.value)} maxLength={6} className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
          {error && <div className="text-xs text-aura-red">{error}</div>}
          <button onClick={confirm} className="btn-primary w-full">Verify and enable</button>
        </>
      ) : (
        <p className="text-sm text-white/50">Loading…</p>
      )}
    </div>
  );
}
