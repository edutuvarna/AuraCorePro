'use client';

import { useEffect, useState } from 'react';
import { Mail, Send, Trash2, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';

interface Invitation {
  tokenHash: string;
  adminEmail: string | null;
  createdByEmail: string | null;
  createdAt: string;
  expiresAt: string;
  consumedAt: string | null;
  status: 'pending' | 'accepted' | 'expired';
}

export function InvitationsPage() {
  const [items, setItems] = useState<Invitation[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const r = await api.listInvitations();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const revoke = async (hash: string) => {
    if (!confirm('Revoke this invitation? The admin account will remain but the setup link stops working.')) return;
    await api.revokeInvitation(hash);
    refresh();
  };
  const resend = async (hash: string) => {
    await api.resendInvitation(hash);
    refresh();
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2">
        <Mail className="w-6 h-6" />Pending Invitations
      </h1>
      <div className="glass-card overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-white/50">Loading…</div>
        ) : items.length === 0 ? (
          <div className="p-8 text-center text-white/50">No invitations.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="p-3 text-left">Email</th>
                <th className="p-3 text-left">Created by</th>
                <th className="p-3 text-left">Status</th>
                <th className="p-3 text-left">Expires</th>
                <th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((i) => (
                <tr key={i.tokenHash} className="border-t border-white/5">
                  <td className="p-3">{i.adminEmail}</td>
                  <td className="p-3 text-white/60">{i.createdByEmail}</td>
                  <td className="p-3">
                    {i.status === 'pending' && <span className="text-aura-yellow">pending</span>}
                    {i.status === 'accepted' && <span className="text-green-400">accepted</span>}
                    {i.status === 'expired' && <span className="text-white/40">expired</span>}
                  </td>
                  <td className="p-3 text-white/50">{new Date(i.expiresAt).toLocaleString()}</td>
                  <td className="p-3 text-right space-x-2">
                    {i.status !== 'accepted' && (
                      <>
                        <button title="Resend email" onClick={() => resend(i.tokenHash)} className="btn-ghost-sm">
                          <Send className="w-3 h-3" />
                        </button>
                        <button title="Revoke" onClick={() => revoke(i.tokenHash)} className="btn-danger-sm">
                          <Trash2 className="w-3 h-3" />
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <button onClick={refresh} className="btn-ghost inline-flex items-center gap-2">
        <RefreshCw className="w-4 h-4" />Refresh
      </button>
    </div>
  );
}
