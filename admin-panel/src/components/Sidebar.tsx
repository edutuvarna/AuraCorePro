'use client';

/**
 * Desktop-only nav sidebar (Phase 6.10 W2.T3 — extracted from page.tsx).
 * Mobile bottom-tab variant ships in Wave 3 Task 15.
 */

import { LucideIcon } from 'lucide-react';

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
}

export function Sidebar({ groups, activePage, onSelect }: SidebarProps) {
    return (
        <aside className="w-[200px] flex-shrink-0 border-r border-white/[0.05] bg-white/[0.02] backdrop-blur-xl p-4 hidden md:flex md:flex-col gap-1">
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
                                onClick={() => onSelect(item.id)}
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
        </aside>
    );
}
