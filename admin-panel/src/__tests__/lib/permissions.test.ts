import { describe, it, expect } from 'vitest';
import {
  PERMISSION_KEYS, TIER1_KEYS, TIER2_KEYS,
  PERMISSION_LABELS, isTabKey, isActionKey,
} from '@/lib/permissions';

describe('permissions', () => {
  it('lists 4 Tier 1 tab keys', () => {
    expect(TIER1_KEYS).toHaveLength(4);
    expect(TIER1_KEYS).toContain('tab:configuration');
    expect(TIER1_KEYS).toContain('tab:ipwhitelist');
    expect(TIER1_KEYS).toContain('tab:updates');
    expect(TIER1_KEYS).toContain('tab:rolechange');
  });

  it('lists 6 Tier 2 action keys', () => {
    expect(TIER2_KEYS).toHaveLength(6);
  });

  it('classifies keys', () => {
    expect(isTabKey('tab:updates')).toBe(true);
    expect(isTabKey('action:users.delete')).toBe(false);
    expect(isActionKey('action:users.delete')).toBe(true);
  });

  it('has human-readable label for every key', () => {
    PERMISSION_KEYS.forEach(k => {
      expect(PERMISSION_LABELS[k]).toBeTruthy();
      expect(PERMISSION_LABELS[k].length).toBeGreaterThan(3);
    });
  });
});
