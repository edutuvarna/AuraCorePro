'use client';

/**
 * PermissionNotificationsProvider — role-gated toast notifications for the
 * four Phase 6.11 permission SignalR events (Bug #7 fix).
 *
 * The backend already broadcasts these events (see signalr.ts header comment);
 * before this provider landed the frontend wasn't subscribed to any of them,
 * so superadmins never saw "new request" toasts and admins never got visible
 * feedback on approve/deny/revoke decisions until they manually reloaded.
 *
 * Role-gating rules:
 *   - PermissionRequested  -> superadmin only (admins are the requester)
 *   - PermissionApproved   -> admin only (superadmins made the decision)
 *   - PermissionDenied     -> admin only
 *   - PermissionRevoked    -> admin only
 */

import { useEffect } from 'react';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { on, off, startConnection } from '@/lib/signalr';
import { useRole } from '@/lib/roleContext';

function fmtDate(iso?: string): string | null {
    if (!iso) return null;
    try {
        return new Date(iso).toLocaleString();
    } catch {
        return null;
    }
}

export function PermissionNotificationsProvider({ children }: { children: ReactNode }) {
    const role = useRole();

    useEffect(() => {
        startConnection();

        const onRequested = (d: any) => {
            if (role !== 'superadmin') return;
            const adminEmail = d?.adminEmail ?? 'unknown admin';
            const permissionKey = d?.permissionKey ?? 'unknown permission';
            toast(
                `New permission request from ${adminEmail}: ${permissionKey}`,
                {
                    duration: 6000,
                    icon: 'i',
                    style: {
                        background: 'rgba(20,20,24,0.9)',
                        color: '#fff',
                        border: '1px solid rgba(6,182,212,0.35)',
                    },
                }
            );
        };

        const onApproved = (d: any) => {
            if (role !== 'admin') return;
            const key = d?.permissionKey ?? 'permission';
            const expires = fmtDate(d?.expiresAt);
            toast.custom((t) => (
                <div
                    className={`glass-card p-3 max-w-sm ${t.visible ? 'animate-slide-up' : 'opacity-0'}`}
                    style={{
                        background: 'rgba(20,20,24,0.9)',
                        color: '#fff',
                        border: '1px solid rgba(16,185,129,0.35)',
                        borderRadius: '12px',
                    }}
                >
                    <div className="font-semibold text-sm text-aura-green">Permission approved: {key}</div>
                    {expires && <div className="text-xs text-white/50 mt-1">expires {expires}</div>}
                </div>
            ), { duration: 6000 });
        };

        const onDenied = (d: any) => {
            if (role !== 'admin') return;
            const key = d?.permissionKey ?? 'permission';
            const reviewNote = typeof d?.reviewNote === 'string' && d.reviewNote.trim().length > 0
                ? d.reviewNote
                : null;
            toast.custom((t) => (
                <div
                    className={`glass-card p-3 max-w-sm ${t.visible ? 'animate-slide-up' : 'opacity-0'}`}
                    style={{
                        background: 'rgba(20,20,24,0.9)',
                        color: '#fff',
                        border: '1px solid rgba(239,68,68,0.35)',
                        borderRadius: '12px',
                    }}
                >
                    <div className="font-semibold text-sm text-aura-red">Permission denied: {key}</div>
                    {reviewNote && <div className="text-xs text-white/50 mt-1">{reviewNote}</div>}
                </div>
            ), { duration: 6000 });
        };

        const onRevoked = (d: any) => {
            if (role !== 'admin') return;
            const key = d?.permissionKey ?? 'permission';
            const reason = typeof d?.reason === 'string' && d.reason.trim().length > 0
                ? d.reason
                : null;
            toast.custom((t) => (
                <div
                    className={`glass-card p-3 max-w-sm ${t.visible ? 'animate-slide-up' : 'opacity-0'}`}
                    style={{
                        background: 'rgba(20,20,24,0.9)',
                        color: '#fff',
                        border: '1px solid rgba(245,158,11,0.35)',
                        borderRadius: '12px',
                    }}
                >
                    <div className="font-semibold text-sm text-aura-amber">Permission revoked: {key}</div>
                    {reason && <div className="text-xs text-white/50 mt-1">{reason}</div>}
                </div>
            ), { duration: 6000 });
        };

        on('PermissionRequested', onRequested);
        on('PermissionApproved', onApproved);
        on('PermissionDenied', onDenied);
        on('PermissionRevoked', onRevoked);

        return () => {
            off('PermissionRequested', onRequested);
            off('PermissionApproved', onApproved);
            off('PermissionDenied', onDenied);
            off('PermissionRevoked', onRevoked);
        };
    }, [role]);

    return <>{children}</>;
}
