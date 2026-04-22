/**
 * Per-page title block with optional breadcrumb + action slot
 * (Phase 6.10 W2.T3 — extracted from page.tsx).
 */

import { ReactNode } from 'react';

export interface PageHeaderProps {
    title: string;
    subtitle?: string;
    breadcrumb?: string;  // e.g. "~/admin/users"
    children?: ReactNode;  // action slot (refresh button, new btn, etc.)
}

export function PageHeader({ title, subtitle, breadcrumb, children }: PageHeaderProps) {
    return (
        <div className="flex justify-between items-start mb-4">
            <div>
                {breadcrumb && (
                    <div className="text-[10px] uppercase tracking-[0.1em] font-mono text-white/40 mb-1">
                        {breadcrumb}
                    </div>
                )}
                <h1 className="text-2xl font-bold tracking-tight bg-gradient-to-br from-zinc-200 to-zinc-500 bg-clip-text text-transparent">
                    {title}
                </h1>
                {subtitle && <div className="text-xs text-white/55 mt-1 font-mono">{subtitle}</div>}
            </div>
            {children && <div className="flex items-center gap-2">{children}</div>}
        </div>
    );
}
