'use client';

import { useState } from 'react';
import { LucideIcon, LogOut, Shield } from 'lucide-react';
import { MobileSheet } from './MobileSheet';

/**
 * Desktop nav sidebar (md:flex) + mobile bottom-tab bar (md:hidden) + "more" grid sheet
 * (Phase 6.10 W3.T15 — extends Phase 6.10 W2.T3 desktop-only Sidebar with mobile shell).
 *
 * Mobile pattern: 4 primary tabs in fixed bottom bar + "more" gradient button → MobileSheet
 * with 2-col grid of all 12 tabs + optional live mini-stats per tab.
 */
export interface NavItem {
    id: string;
    icon: LucideIcon;
    label: string;
}
export interface NavGroup {
    title: string;
    items: NavItem[];
}
export interface SidebarProps {
    groups: NavGroup[];
    activePage: string;
    onSelect: (page: string) => void;
    /** 4 tabs to show in bottom bar; rest go in "more" sheet. */
    primaryMobileTabIds?: string[];
    /** Optional per-tab mini-stats rendered inside the "more" grid sheet cards. */
    miniStats?: Record<string, { value: string; meta: string; alert?: boolean }>;
    /** Sign out callback. When provided, renders a logout button in desktop sidebar footer + mobile "more" sheet. */
    onLogout?: () => void;
    /** Optional label shown above the logout button (e.g. user's email). */
    currentUserEmail?: string;
    /** Open the MyPermissions page. When provided, renders a button in desktop footer + mobile more-sheet. */
    onOpenMyPermissions?: () => void;
}

export function Sidebar({
    groups,
    activePage,
    onSelect,
    primaryMobileTabIds = ['dashboard', 'users', 'licenses', 'payments'],
    miniStats = {},
    onLogout,
    currentUserEmail,
    onOpenMyPermissions,
}: SidebarProps) {
    const [sheetOpen, setSheetOpen] = useState(false);
    const allItems: NavItem[] = groups.flatMap((g) => g.items);
    const primaryItems = primaryMobileTabIds
        .map((id) => allItems.find((i) => i.id === id))
        .filter((i): i is NavItem => !!i);

    const handleSelect = (id: string) => {
        onSelect(id);
        setSheetOpen(false);
    };

    return (
        <>
            {/* Desktop sidebar */}
            <aside className="w-[200px] flex-shrink-0 border-r border-white/[0.05] bg-white/[0.02] backdrop-blur-xl p-4 hidden md:flex md:flex-col gap-1 h-full overflow-y-auto">
                <div className="flex items-center gap-2 mb-4 px-2 font-mono text-sm">
                    <div className="w-5 h-5 rounded-md bg-gradient-to-br from-cyan-500 to-purple-500 shadow-[0_0_12px_rgba(6,182,212,0.5)]" />
                    <span>auracore.admin</span>
                </div>
                {groups.map((group) => (
                    <div key={group.title} className="flex flex-col gap-1 mb-3">
                        {group.items.map((item) => {
                            const Icon = item.icon;
                            const isActive = activePage === item.id;
                            return (
                                <button
                                    key={item.id}
                                    onClick={() => handleSelect(item.id)}
                                    className={`flex items-center gap-2.5 px-2.5 py-2 rounded-md text-xs font-mono text-left transition-colors ${
                                        isActive
                                            ? 'bg-cyan-500/10 text-cyan-400 border border-cyan-500/25'
                                            : 'text-white/55 hover:bg-white/[0.03] hover:text-white/80 border border-transparent'
                                    }`}
                                >
                                    <Icon className="w-3.5 h-3.5 opacity-80" />
                                    <span>{item.label}</span>
                                    <span className={`ml-auto text-[10px] ${isActive ? 'opacity-90' : 'opacity-30'}`}>→</span>
                                </button>
                            );
                        })}
                    </div>
                ))}
                {(onLogout || onOpenMyPermissions) && (
                    <div className="mt-auto pt-3 border-t border-white/[0.05]">
                        {currentUserEmail && (
                            <div className="px-2.5 pb-2 text-[10px] font-mono text-white/35 truncate" title={currentUserEmail}>
                                {currentUserEmail}
                            </div>
                        )}
                        {onOpenMyPermissions && (
                            <button
                                onClick={onOpenMyPermissions}
                                className="w-full flex items-center gap-2.5 px-2.5 py-2 rounded-md text-xs font-mono text-left text-white/55 hover:bg-white/[0.05] hover:text-white/90 border border-transparent transition-colors"
                            >
                                <Shield className="w-3.5 h-3.5 opacity-80" />
                                <span>My Permissions</span>
                            </button>
                        )}
                        {onLogout && (
                            <button
                                onClick={onLogout}
                                className="w-full flex items-center gap-2.5 px-2.5 py-2 rounded-md text-xs font-mono text-left text-white/55 hover:bg-red-500/[0.08] hover:text-red-400 border border-transparent hover:border-red-500/20 transition-colors"
                            >
                                <LogOut className="w-3.5 h-3.5 opacity-80" />
                                <span>Sign out</span>
                            </button>
                        )}
                    </div>
                )}
            </aside>

            {/* Mobile bottom tab bar */}
            <nav className="fixed bottom-0 left-0 right-0 z-30 h-16 bg-[#08080c]/[0.92] backdrop-blur-2xl border-t border-white/[0.05] flex p-2 gap-1 md:hidden">
                {primaryItems.map((item) => {
                    const Icon = item.icon;
                    const isActive = activePage === item.id;
                    return (
                        <button
                            key={item.id}
                            onClick={() => handleSelect(item.id)}
                            className={`flex-1 flex flex-col items-center justify-center gap-1 rounded-lg text-[9px] font-mono transition-all ${
                                isActive ? 'text-cyan-400 bg-cyan-500/[0.06]' : 'text-white/45'
                            }`}
                        >
                            <Icon className={`w-[18px] h-[18px] ${isActive ? 'drop-shadow-[0_0_8px_rgba(6,182,212,0.5)]' : ''}`} />
                            <span>{item.label}</span>
                        </button>
                    );
                })}
                <button
                    onClick={() => setSheetOpen(true)}
                    className="flex-1 flex flex-col items-center justify-center gap-1 rounded-lg text-[9px] font-mono text-white/55"
                >
                    <div className="w-[18px] h-[18px] rounded bg-gradient-to-br from-cyan-500 to-purple-500 opacity-85" />
                    <span>more</span>
                </button>
            </nav>

            {/* "More" grid sheet — MobileSheet API exposes only title (no subtitle); section-count
                hint folded into the title string instead. */}
            <MobileSheet open={sheetOpen} onClose={() => setSheetOpen(false)} title={`All sections · ${allItems.length} tabs`}>
                {onLogout && currentUserEmail && (
                    <div className="mb-2 px-1 text-[10px] font-mono text-white/35 truncate">Signed in as {currentUserEmail}</div>
                )}
                <div className="grid grid-cols-2 gap-2.5 pb-16">
                    {allItems.map((item) => {
                        const Icon = item.icon;
                        const stat = miniStats[item.id];
                        const isAlert = stat?.alert;
                        const isActive = activePage === item.id;
                        return (
                            <button
                                key={item.id}
                                onClick={() => handleSelect(item.id)}
                                className={`p-3 rounded-xl border flex flex-col gap-1.5 min-h-[70px] text-left transition-all ${
                                    isActive
                                        ? 'border-cyan-500/30 bg-cyan-500/[0.04]'
                                        : isAlert
                                        ? 'border-red-500/30 bg-red-500/[0.04]'
                                        : 'border-white/[0.06] bg-white/[0.03]'
                                }`}
                            >
                                <div className="flex items-center gap-2">
                                    <Icon className={`w-5 h-5 rounded ${isActive ? 'text-cyan-400' : isAlert ? 'text-red-400' : 'text-white/60'}`} />
                                    <span className={`text-xs font-mono flex-1 ${isActive ? 'text-cyan-400' : 'text-white/85'}`}>{item.label}</span>
                                </div>
                                {stat && (
                                    <>
                                        <div className={`text-xs font-mono ${isAlert ? 'text-red-400' : 'text-cyan-400'} font-medium`}>{stat.value}</div>
                                        <div className="text-[9px] opacity-45 font-mono">{stat.meta}</div>
                                    </>
                                )}
                            </button>
                        );
                    })}
                    {onOpenMyPermissions && (
                        <button
                            onClick={() => { onOpenMyPermissions(); setSheetOpen(false); }}
                            className="p-3 rounded-xl border border-white/[0.06] bg-white/[0.03] flex flex-col gap-1.5 min-h-[70px] text-left col-span-2"
                        >
                            <div className="flex items-center gap-2">
                                <Shield className="w-5 h-5 rounded text-white/60" />
                                <span className="text-xs font-mono flex-1 text-white/85">My Permissions</span>
                            </div>
                        </button>
                    )}
                    {onLogout && (
                        <button
                            onClick={() => { onLogout(); setSheetOpen(false); }}
                            className="p-3 rounded-xl border border-red-500/20 bg-red-500/[0.04] flex flex-col gap-1.5 min-h-[70px] text-left col-span-2"
                        >
                            <div className="flex items-center gap-2">
                                <LogOut className="w-5 h-5 rounded text-red-400" />
                                <span className="text-xs font-mono flex-1 text-red-400">Sign out</span>
                            </div>
                        </button>
                    )}
                </div>
            </MobileSheet>
        </>
    );
}
