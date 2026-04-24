'use client';

import { createContext, useContext } from 'react';
import type { UserRole } from '@/lib/types';

/**
 * Exposes the current admin's role ('admin' | 'superadmin') to every page
 * rendered inside AdminPanelInner. Prefer Context over prop-drilling — the
 * PAGES record otherwise has to propagate `role` into 23 view components
 * that mostly ignore it.
 *
 * Phase 6.11 W3.T23 — wrapped around AdminPanelInner's return; views call
 * `useRole()` and thread the result into `usePermissions(role)`.
 */
export const RoleContext = createContext<UserRole>('admin');

export const useRole = () => useContext(RoleContext);
