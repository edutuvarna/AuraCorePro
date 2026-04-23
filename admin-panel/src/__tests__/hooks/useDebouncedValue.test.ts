import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';

describe('useDebouncedValue', () => {
    it('returns initial value immediately', () => {
        const { result } = renderHook(() => useDebouncedValue('hello', 100));
        expect(result.current).toBe('hello');
    });

    it('debounces rapid value updates', async () => {
        const { result, rerender } = renderHook(
            ({ value }) => useDebouncedValue(value, 50),
            { initialProps: { value: 'a' } }
        );
        rerender({ value: 'b' });
        rerender({ value: 'c' });
        // Still old value before delay elapses
        expect(result.current).toBe('a');
        await waitFor(() => expect(result.current).toBe('c'), { timeout: 500 });
    });

    it('uses default 400ms delay when not specified', () => {
        const { result } = renderHook(() => useDebouncedValue(42));
        expect(result.current).toBe(42);
    });
});
