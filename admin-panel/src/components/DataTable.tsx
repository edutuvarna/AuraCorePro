'use client';

import { ReactNode } from 'react';
import { useMediaQuery } from '@/hooks/useMediaQuery';

/**
 * Responsive data-table primitive (Phase 6.10 W2.T11).
 *
 * Renders a desktop `<table>` above the `(min-width: 768px)` breakpoint and a
 * card-list (one card per row, label/value pairs) below it. Card mode uses the
 * `headerLabel` / `mobileLabel` callbacks so consumers can pick the column to
 * promote to the card title and how to format each value.
 *
 * No consumers in Task 11 — Wave 3 Task 16 wires the first migration target.
 * Created here so the lift commit owns every primitive in one place.
 */
export interface DataTableColumn<T> {
    key: string;
    header: string;
    render: (row: T) => ReactNode;
    /** Optional: cell className override on desktop (`<td>` className). */
    cellClassName?: string;
    /** Optional: shown as the card's title in mobile mode. Exactly one column should set this. */
    isCardTitle?: boolean;
}

export interface DataTableProps<T> {
    columns: DataTableColumn<T>[];
    rows: T[];
    /** Stable key extractor — needed so React reconciliation works through sort/filter. */
    rowKey: (row: T) => string | number;
    /** Optional click handler — gets called for both desktop row click and mobile card tap. */
    onRowClick?: (row: T) => void;
    /** Renders below the rows (e.g. for pagination controls). */
    footer?: ReactNode;
    /** Renders when `rows.length === 0`. Pass an `<EmptyState>` here. */
    emptyState?: ReactNode;
    /** Override the desktop breakpoint. Defaults to Tailwind's `md` = 768px. */
    desktopMinWidth?: number;
}

export function DataTable<T>({
    columns,
    rows,
    rowKey,
    onRowClick,
    footer,
    emptyState,
    desktopMinWidth = 768,
}: DataTableProps<T>) {
    const isDesktop = useMediaQuery(`(min-width: ${desktopMinWidth}px)`);

    if (rows.length === 0 && emptyState) {
        return <>{emptyState}</>;
    }

    if (isDesktop) {
        return (
            <div className="overflow-x-auto">
                <table className="w-full text-sm">
                    <thead>
                        <tr className="text-left text-[11px] font-semibold text-white/35 uppercase tracking-wider border-b border-white/5">
                            {columns.map(c => (
                                <th key={c.key} className="py-3 px-4">{c.header}</th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map(row => (
                            <tr
                                key={rowKey(row)}
                                onClick={onRowClick ? () => onRowClick(row) : undefined}
                                className={`border-b border-white/[0.04] hover:bg-white/[0.02] ${onRowClick ? 'cursor-pointer' : ''}`}
                            >
                                {columns.map(c => (
                                    <td key={c.key} className={`py-3 px-4 ${c.cellClassName ?? ''}`}>
                                        {c.render(row)}
                                    </td>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
                {footer}
            </div>
        );
    }

    // Mobile: card list
    const titleColumn = columns.find(c => c.isCardTitle) ?? columns[0];
    const detailColumns = columns.filter(c => c !== titleColumn);

    return (
        <div className="space-y-3">
            {rows.map(row => (
                <div
                    key={rowKey(row)}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                    className={`glass-card p-4 ${onRowClick ? 'cursor-pointer hover:bg-white/[0.04]' : ''}`}
                >
                    <div className="font-medium text-white/90 mb-2">{titleColumn.render(row)}</div>
                    <div className="space-y-1.5">
                        {detailColumns.map(c => (
                            <div key={c.key} className="flex items-center justify-between text-xs">
                                <span className="text-white/35 uppercase tracking-wider">{c.header}</span>
                                <span className="text-white/70">{c.render(row)}</span>
                            </div>
                        ))}
                    </div>
                </div>
            ))}
            {footer}
        </div>
    );
}
