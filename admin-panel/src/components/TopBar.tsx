/**
 * Mobile-only top bar with menu trigger + page name + refresh action
 * (Phase 6.10 W2.T3 — created as a slot for Wave 3 mobile shell wiring).
 * Desktop layout uses Sidebar instead.
 */

import { Menu, RefreshCw } from 'lucide-react';

export interface TopBarProps {
    pageName: string;
    onMenuClick?: () => void;
    onRefreshClick?: () => void;
}

export function TopBar({ pageName, onMenuClick, onRefreshClick }: TopBarProps) {
    return (
        <header className="flex items-center justify-between px-3.5 py-2.5 border-b border-white/[0.05] md:hidden">
            {onMenuClick && (
                <button
                    onClick={onMenuClick}
                    className="w-8 h-8 bg-white/[0.04] border border-white/[0.08] rounded-md flex items-center justify-center"
                >
                    <Menu className="w-3.5 h-3.5 text-white/70" />
                </button>
            )}
            <div className="font-mono text-xs text-white/85 flex gap-1">
                <span className="text-white/35">~/admin/</span>
                <span>{pageName}</span>
            </div>
            <div className="flex gap-1.5">
                {onRefreshClick && (
                    <button
                        onClick={onRefreshClick}
                        className="w-7.5 h-7.5 bg-white/[0.04] border border-white/[0.08] rounded-md flex items-center justify-center text-white/70"
                    >
                        <RefreshCw className="w-3.5 h-3.5" />
                    </button>
                )}
            </div>
        </header>
    );
}
