'use client';

import { useEffect, useMemo, useState } from 'react';
import { api } from '@/lib/api';
import type { UserRole, MyPermissionsResponse } from '@/lib/types';

/**
 * Fetches the caller's grants from /api/admin/my-permissions and returns a
 * simple has()/hasPending() predicate plus a refresh() callback.
 *
 * Superadmins short-circuit: has() always returns true and no fetch is made.
 *
 * Phase 6.11 W3.T23 — drives `<PermissionGate>` wrappers around destructive
 * buttons (Users/Subscriptions/Payments) and `<LockedTabPlaceholder>` renders
 * on Tier 1 tab pages (wired in T24).
 */
export function usePermissions(role: UserRole) {
  const [data, setData] = useState<MyPermissionsResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (role === 'superadmin') { setLoading(false); return; }
    let alive = true;
    api.getMyPermissions().then((d) => {
      if (alive) { setData(d); setLoading(false); }
    });
    return () => { alive = false; };
  }, [role]);

  const activeSet = useMemo(() => {
    if (role === 'superadmin') return null; // sentinel — has() always true
    const now = Date.now();
    return new Set(
      (data?.grants ?? [])
        .filter((g) => !g.expiresAt || new Date(g.expiresAt).getTime() > now)
        .map((g) => g.permissionKey)
    );
  }, [data, role]);

  const pendingSet = useMemo(
    () => new Set((data?.pending ?? []).map((p) => p.permissionKey)),
    [data]
  );

  return {
    loading,
    data,
    has: (key: string) => (activeSet === null ? true : activeSet.has(key)),
    hasPending: (key: string) => pendingSet.has(key),
    refresh: async () => {
      setLoading(true);
      setData(await api.getMyPermissions());
      setLoading(false);
    },
  };
}
