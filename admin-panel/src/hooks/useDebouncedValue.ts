'use client';

import { useEffect, useState } from 'react';

/**
 * Phase 6.9 T2.6 — delays propagation of a rapidly-changing value
 * (e.g. search box input). Input stays instantly responsive; the
 * debounced value settles after delayMs of no change. Default 400ms.
 */
export function useDebouncedValue<T>(value: T, delayMs: number = 400): T {
    const [debounced, setDebounced] = useState(value);
    useEffect(() => {
        const handle = setTimeout(() => setDebounced(value), delayMs);
        return () => clearTimeout(handle);
    }, [value, delayMs]);
    return debounced;
}
