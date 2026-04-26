import { createContext, useContext, ReactNode } from 'react';
import type { Role } from './auth';

const RoleContext = createContext<Role>('admin');

export function RoleProvider({ value, children }: { value: Role; children: ReactNode }) {
  return <RoleContext.Provider value={value}>{children}</RoleContext.Provider>;
}

export const useRole = () => useContext(RoleContext);
