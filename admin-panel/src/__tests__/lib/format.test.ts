import { describe, it, expect } from 'vitest';
import { formatCurrency, formatBytes, formatDate } from '@/lib/format';

describe('formatCurrency', () => {
    it('returns dash for null/undefined amount', () => {
        expect(formatCurrency(null, 'USD')).toBe('—');
        expect(formatCurrency(undefined, 'USD')).toBe('—');
    });
    it('formats USD with 4.99 substring', () => {
        const result = formatCurrency(4.99, 'USD');
        expect(result).toContain('4.99');
    });
    it('falls back to plain format for unknown currency', () => {
        const result = formatCurrency(100, 'XXX');
        expect(result).toContain('100.00');
    });
    it('parses string amount', () => {
        const result = formatCurrency('12.50', 'USD');
        expect(result).toContain('12.50');
    });
    it('returns dash for non-finite numeric', () => {
        expect(formatCurrency('not-a-number', 'USD')).toBe('—');
    });
});

describe('formatBytes', () => {
    it('returns dash for null/undefined', () => {
        expect(formatBytes(null)).toBe('—');
        expect(formatBytes(undefined)).toBe('—');
    });
    it('formats KB for 1024 bytes', () => {
        expect(formatBytes(1024)).toBe('1.0 KB');
    });
    it('formats MB for large values', () => {
        expect(formatBytes(1024 * 1024 * 5)).toBe('5.0 MB');
    });
    it('formats raw bytes (no decimal) for sub-KB values', () => {
        expect(formatBytes(512)).toBe('512 B');
    });
});

describe('formatDate', () => {
    it('returns dash for null/undefined', () => {
        expect(formatDate(null)).toBe('—');
        expect(formatDate(undefined)).toBe('—');
    });
    it('returns dash for invalid date string', () => {
        expect(formatDate('not-a-date')).toBe('—');
    });
    it('returns a non-dash string for a valid ISO date', () => {
        const result = formatDate('2026-04-23T10:00:00Z');
        expect(result).not.toBe('—');
        expect(result.length).toBeGreaterThan(0);
    });
});
