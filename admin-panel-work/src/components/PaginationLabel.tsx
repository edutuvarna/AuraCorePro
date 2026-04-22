export interface PaginationLabelProps {
    page: number;
    pageSize: number;
    total: number;
}

/**
 * Phase 6.9 T2.5 — shared "Showing 1–50 of 124" label for list views.
 * Renders "No results" when total is 0.
 */
export function PaginationLabel({ page, pageSize, total }: PaginationLabelProps) {
    if (total === 0) return <span className="text-zinc-500 text-sm">No results</span>;
    const start = (page - 1) * pageSize + 1;
    const end = Math.min(page * pageSize, total);
    return <span className="text-zinc-400 text-sm">Showing {start}–{end} of {total}</span>;
}
