'use client';

import { useEffect, useState } from 'react';
import { UserCog, UserPlus, Ban, RotateCw, Key, Trash2 } from 'lucide-react';
import { api } from '@/lib/api';
import type { AdminAccount } from '@/lib/types';
import { CreateAdminModal } from '@/components/CreateAdminModal';

export function AdminManagementPage() {
  const [items, setItems] = useState<AdminAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState<'create'|null>(null);

  const refresh = async () => {
    setLoading(true);
    const r = await api.listAdminAccounts();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const onSuspend = async (id: string) => { await api.suspendAdmin(id); refresh(); };
  const onRestore = async (id: string) => { await api.restoreAdmin(id); refresh(); };
  const onReset = async (id: string) => { await api.resetAdminPassword(id); alert('Reset link emailed.'); };
  const onDelete = async (id: string) => { if (!confirm('Delete this admin? This is permanent.')) return; await api.deleteAdmin(id); refresh(); };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><UserCog className="w-6 h-6" />Admin Management</h1>
        <button onClick={() => setModal('create')} className="btn-primary inline-flex items-center gap-2"><UserPlus className="w-4 h-4" />+ Create Admin</button>
      </div>

      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="p-3 text-left">Email</th>
                <th className="p-3 text-left">Role</th>
                <th className="p-3 text-left">Active</th>
                <th className="p-3 text-left">Readonly</th>
                <th className="p-3 text-left">2FA</th>
                <th className="p-3 text-left">Created</th>
                <th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(a => (
                <tr key={a.id} className="border-t border-white/5">
                  <td className="p-3">{a.email}</td>
                  <td className="p-3">{a.role}</td>
                  <td className="p-3">{a.isActive ? '✓' : <span className="text-aura-red">suspended</span>}</td>
                  <td className="p-3">{a.isReadonly ? 'yes' : 'no'}</td>
                  <td className="p-3">{a.totpEnabled ? 'on' : 'off'}</td>
                  <td className="p-3 text-white/50">{new Date(a.createdAt).toLocaleDateString()}</td>
                  <td className="p-3 text-right space-x-2">
                    <button title="Reset password" onClick={() => onReset(a.id)} className="btn-ghost-sm"><Key className="w-3 h-3" /></button>
                    {a.isActive
                      ? <button title="Suspend" onClick={() => onSuspend(a.id)} className="btn-ghost-sm"><Ban className="w-3 h-3" /></button>
                      : <button title="Restore" onClick={() => onRestore(a.id)} className="btn-ghost-sm"><RotateCw className="w-3 h-3" /></button>}
                    {a.role === 'admin' && (
                      <button title="Delete" onClick={() => onDelete(a.id)} className="btn-danger-sm"><Trash2 className="w-3 h-3" /></button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal === 'create' && <CreateAdminModal onClose={() => setModal(null)} onCreated={refresh} />}
    </div>
  );
}
