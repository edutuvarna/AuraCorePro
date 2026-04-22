/**
 * Phase 6.9 CTP-8: centralized currency formatting.
 * Uses Intl.NumberFormat for locale-aware output.
 *
 * Backend stores Currency as 3-letter ISO uppercase (USD, EUR, TRY).
 * This function accepts that and produces a user-visible string like
 * "$4.99", "€49.00", "₺149,00" per locale default.
 */
export function formatCurrency(amount: number | string | null | undefined, currency: string | null | undefined): string {
    if (amount === null || amount === undefined) return '—';
    const num = typeof amount === 'string' ? parseFloat(amount) : amount;
    if (!Number.isFinite(num)) return '—';

    const code = (currency ?? 'USD').toUpperCase();
    try {
        return new Intl.NumberFormat(undefined, {
            style: 'currency',
            currency: code,
        }).format(num);
    } catch {
        // Unknown currency code (bad data) — fall back to plain with code suffix
        return `${num.toFixed(2)} ${code}`;
    }
}

/**
 * Format bytes to human-readable (1.23 MB, 456 KB, etc).
 */
export function formatBytes(bytes: number | null | undefined): string {
    if (bytes === null || bytes === undefined || !Number.isFinite(bytes)) return '—';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = Math.abs(bytes);
    let unitIdx = 0;
    while (value >= 1024 && unitIdx < units.length - 1) {
        value /= 1024;
        unitIdx++;
    }
    return `${value.toFixed(unitIdx === 0 ? 0 : 1)} ${units[unitIdx]}`;
}

/**
 * Format a date/timestamp to concise local format.
 */
export function formatDate(input: string | Date | null | undefined): string {
    if (!input) return '—';
    const d = typeof input === 'string' ? new Date(input) : input;
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleString(undefined, {
        year: 'numeric', month: 'short', day: '2-digit',
        hour: '2-digit', minute: '2-digit',
    });
}
