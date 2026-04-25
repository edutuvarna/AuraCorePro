'use client';

import { Lock, Send, Clock } from 'lucide-react';

export interface LockedTabPlaceholderProps {
  tabName: string;
  permissionKey: string;
  onRequestStart?: (key: string) => void;
  hasPending?: boolean;
  pendingAt?: string;
  lastDenial?: { reviewNote?: string | null; reviewedAt: string };
  /**
   * When set, renders a hard-restriction view with this message instead of the
   * default permission-request copy + button. Used by tabs whose backend
   * authorization is hardcoded (e.g. role-based) so a permission-grant UI
   * would mislead the user.
   */
  staticMessage?: string;
}

export function LockedTabPlaceholder({
  tabName, permissionKey, onRequestStart, hasPending, pendingAt, lastDenial, staticMessage,
}: LockedTabPlaceholderProps) {
  return (
    <div className="flex items-center justify-center min-h-[50vh]">
      <div className="max-w-md text-center space-y-6">
        <div className="inline-flex items-center justify-center w-20 h-20 rounded-3xl bg-accent/10 border border-accent/20 mx-auto">
          <Lock className="w-10 h-10 text-accent" />
        </div>
        <div className="space-y-2">
          <h2 className="text-xl font-display font-bold">{tabName} is locked</h2>
          <p className="text-sm text-white/60 leading-relaxed">
            {staticMessage || (
              <>This page has been disabled by the superadmin by default. You need permission
              from the superadmin to be able to use the {tabName} tab.</>
            )}
          </p>
        </div>
        {!staticMessage && (
          hasPending ? (
            <div className="flex items-center gap-2 justify-center text-sm text-white/50 bg-white/5 border border-white/10 rounded-xl px-4 py-3">
              <Clock className="w-4 h-4" />
              Pending request from {pendingAt ? new Date(pendingAt).toLocaleString() : 'recently'}, awaiting review.
            </div>
          ) : (
            <button onClick={() => onRequestStart?.(permissionKey)}
              className="btn-primary inline-flex items-center gap-2">
              <Send className="w-4 h-4" />
              Request Permission
            </button>
          )
        )}
        {!staticMessage && lastDenial && (
          <div className="text-xs text-aura-red/80 bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-2">
            Last request denied: {lastDenial.reviewNote || 'no reason given'}
          </div>
        )}
      </div>
    </div>
  );
}
