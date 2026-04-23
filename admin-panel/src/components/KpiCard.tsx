'use client';

import { ArrowUpRight, ArrowDownRight } from 'lucide-react';

/**
 * Shared KPI card primitive (Phase 6.10 W2.T11 — lifted from DashboardPage / UsersPage / LicensesPage / etc.).
 *
 * Naming: file is `KpiCard.tsx` per Plan-task-11 example, but the exported
 * component preserves the inherited `KPICard` identifier so call sites can
 * `import { KPICard } from '@/components/KpiCard'` with zero rename churn.
 *
 * Prop signature is the inherited `{ label, value, icon, color, trend, sub, span }`
 * shape (Decision (B) per task spec — match-inherited to minimize churn). The
 * plan's `{ delta, deltaPositive }` example is NOT used; `trend` (signed
 * number, sign drives arrow + color) carries the same information in fewer
 * props and matches every existing call site verbatim.
 *
 * Behavior is unchanged from the inline-code Phase 6.9 hotfix versions —
 * styling tokens, `glass-card-hover`, color-swatch background mapping, the
 * `span === 2 ? 'col-span-2' : ''` grid hint, and the `stat-value` value text
 * all carry forward exactly.
 */
export interface KPICardProps {
    label: string;
    value: string | number;
    icon: any;
    color?: string;
    trend?: number;
    sub?: string;
    span?: number;
}

export function KPICard({
    label,
    value,
    icon: Icon,
    color = 'text-accent',
    trend,
    sub,
    span = 1,
}: KPICardProps) {
    return (
        <div className={`glass-card-hover p-5 ${span === 2 ? 'col-span-2' : ''}`}>
            <div className="flex items-start justify-between mb-3">
                <span className="text-[11px] font-semibold text-white/35 uppercase tracking-wider">{label}</span>
                <div className={`w-9 h-9 rounded-xl ${color.includes('accent') ? 'bg-accent/10' : color.includes('green') ? 'bg-aura-green/10' : color.includes('purple') ? 'bg-aura-purple/10' : color.includes('amber') ? 'bg-aura-amber/10' : color.includes('blue') ? 'bg-aura-blue/10' : color.includes('red') ? 'bg-aura-red/10' : 'bg-white/5'} flex items-center justify-center`}>
                    <Icon className={`w-[18px] h-[18px] ${color}`} />
                </div>
            </div>
            <div className="stat-value">{value}</div>
            <div className="flex items-center gap-2 mt-2">
                {trend !== undefined && (
                    <span className={`flex items-center gap-0.5 text-xs font-medium ${trend >= 0 ? 'text-aura-green' : 'text-aura-red'}`}>
                        {trend >= 0 ? <ArrowUpRight className="w-3 h-3" /> : <ArrowDownRight className="w-3 h-3" />}
                        {Math.abs(trend)}%
                    </span>
                )}
                {sub && <span className="text-xs text-white/30">{sub}</span>}
            </div>
        </div>
    );
}
