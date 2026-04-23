/**
 * Shared TypeScript interfaces for backend API response shapes.
 * Mirrors backend DTOs — kept in sync manually until codegen later.
 *
 * Phase 6.10 W2.T13 (final wave 2 cleanup) — initial seed. Per-page light
 * type adoption is intentionally minimal (only swap `useState<any>` to typed
 * version when it's a 1-line change with no cascading effects). Full typing
 * pass deferred to Wave 5 polish.
 *
 * NOTE on `License.deviceCount`: Phase 6.10 W5.T25 retired the Phase 6.8
 * `activeDevices` backend alias; `deviceCount` is now the single canonical name.
 */

export interface User {
    id: string;
    email: string;
    role: string;
    createdAt: string;
    tier?: string;  // CTP-1 top-level tier from Phase 6.8
    license?: { tier: string; expiresAt: string };  // back-compat nested
}

export interface UsersResponse {
    total: number;
    page: number;
    pageSize: number;
    pages: number;
    users: User[];
}

export interface License {
    id: string;
    key: string;
    tier: string;
    status: string;
    maxDevices: number;
    deviceCount: number;  // Phase 6.10 W5.T25 — single canonical name (activeDevices alias retired)
    createdAt: string;
    expiresAt?: string;
    userId: string;
    userEmail?: string;
}

export interface ListResponse<T> {
    total: number;
    page: number;
    pageSize: number;
    pages: number;
    items: T[];
}

export interface AuditLogEntry {
    id: number;
    actorEmail: string;
    actorId?: string;
    action: string;
    targetType: string;
    targetId?: string;
    createdAt: string;
    ipAddress?: string;
}
