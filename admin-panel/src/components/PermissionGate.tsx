'use client';

import { Lock } from 'lucide-react';
import { PERMISSION_LABELS, PermissionKey } from '@/lib/permissions';

export interface PermissionGateProps {
  permissionKey: string;
  hasPermission: boolean;
  onRequestStart: (key: string) => void;
  children: React.ReactNode;
}

export function PermissionGate({ permissionKey, hasPermission, onRequestStart, children }: PermissionGateProps) {
  if (hasPermission) return <>{children}</>;

  const label = PERMISSION_LABELS[permissionKey as PermissionKey] ?? permissionKey;
  return (
    <button
      type="button"
      onClick={() => onRequestStart(permissionKey)}
      title={`This action requires superadmin permission. Click to request: ${label}.`}
      className="btn-ghost inline-flex items-center gap-1 text-white/40 hover:text-white/70 cursor-pointer"
    >
      <Lock className="w-4 h-4" /> Locked
    </button>
  );
}
