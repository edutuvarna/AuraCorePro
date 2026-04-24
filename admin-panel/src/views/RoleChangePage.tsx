'use client';

import { useState } from 'react';
import { api } from '@/lib/api';
import { useRole } from '@/lib/roleContext';
import { usePermissions } from '@/hooks/usePermissions';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';

export function RoleChangePage() {
  const role = useRole();
  const { has, hasPending } = usePermissions(role);
  const [reqOpen, setReqOpen] = useState(false);

  if (role === 'admin' && !has('tab:rolechange')) {
    return (
      <>
        <LockedTabPlaceholder
          tabName="Role Change"
          permissionKey="tab:rolechange"
          hasPending={hasPending('tab:rolechange')}
          onRequestStart={() => setReqOpen(true)}
        />
        {reqOpen && (
          <PermissionRequestDialog
            isOpen permissionKey="tab:rolechange"
            onClose={() => setReqOpen(false)}
            onSubmit={async (k, r) => (await api.createPermissionRequest(k, r)).ok}
          />
        )}
      </>
    );
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold">Role Change</h1>
      <p className="text-sm text-white/60">Coming in Wave 4.</p>
    </div>
  );
}
