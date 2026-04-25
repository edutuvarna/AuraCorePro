'use client';

import { ShieldAlert, LogOut } from 'lucide-react';

export interface ScopeLimitedBannerProps {
  scope: '2fa-setup-only' | 'change-password';
  onLogout: () => void;
}

export function ScopeLimitedBanner({ scope, onLogout }: ScopeLimitedBannerProps) {
  const message = scope === '2fa-setup-only'
    ? 'Complete two-factor authentication setup to access the rest of the panel.'
    : 'Change your password to access the rest of the panel.';
  return (
    <div className="sticky top-0 z-40 bg-amber-500/10 border-b border-amber-500/30 px-4 py-3 flex items-center justify-between">
      <div className="flex items-center gap-2 text-sm text-amber-200">
        <ShieldAlert className="w-4 h-4 shrink-0" />
        <span>{message}</span>
      </div>
      <button onClick={onLogout} className="btn-ghost flex items-center gap-1.5 text-xs">
        <LogOut className="w-3.5 h-3.5" />
        Sign out
      </button>
    </div>
  );
}
