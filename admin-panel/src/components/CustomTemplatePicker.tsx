'use client';

import { useState } from 'react';
import { PERMISSION_LABELS, TIER1_KEYS, TIER2_KEYS, PermissionKey } from '@/lib/permissions';

export interface CustomKey { permissionKey: string; expiresAt: string | null; }

export function CustomTemplatePicker({
  onChange, readOnly = false,
}: {
  onChange: (keys: CustomKey[]) => void;
  readOnly?: boolean;
}) {
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [defaultExpiry, setDefaultExpiry] = useState<string>('');
  const [perKeyExpiry] = useState<Record<string, string>>({});

  const update = (nextSelected: Record<string, boolean>) => {
    const keys: CustomKey[] = Object.entries(nextSelected)
      .filter(([, v]) => v)
      .map(([k]) => ({ permissionKey: k, expiresAt: perKeyExpiry[k] || defaultExpiry || null }));
    onChange(keys);
  };

  const toggle = (k: string) => {
    const next = { ...selected, [k]: !selected[k] };
    setSelected(next);
    update(next);
  };

  return (
    <div className="space-y-4">
      <div>
        <label className="text-xs text-white/50 block mb-2">Tier 1 — Tabs</label>
        <div className="space-y-1">
          {TIER1_KEYS.map(k => (
            <label key={k} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={!!selected[k]} onChange={() => toggle(k)} disabled={readOnly} />
              <code className="text-xs">{k}</code>
              <span className="text-white/60">— {PERMISSION_LABELS[k as PermissionKey]}</span>
            </label>
          ))}
        </div>
      </div>
      <div>
        <label className="text-xs text-white/50 block mb-2">Tier 2 — Actions</label>
        <div className="space-y-1">
          {TIER2_KEYS.map(k => (
            <label key={k} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={!!selected[k]} onChange={() => toggle(k)} disabled={readOnly} />
              <code className="text-xs">{k}</code>
              <span className="text-white/60">— {PERMISSION_LABELS[k as PermissionKey]}</span>
            </label>
          ))}
        </div>
      </div>
      <div>
        <label className="text-xs text-white/50 block mb-2">Default expiry for checked keys (optional)</label>
        <input type="datetime-local" value={defaultExpiry}
          onChange={e => { setDefaultExpiry(e.target.value); update({ ...selected }); }}
          className="input-dark" disabled={readOnly} />
      </div>
    </div>
  );
}
