'use client';

import { useState, useRef, useEffect, useId, useCallback } from 'react';
import { ChevronDown, Check } from 'lucide-react';

export interface ComboboxOption {
  value: string;
  label: string;
}

export interface ComboboxProps {
  value: string;
  onChange: (value: string) => void;
  options: ComboboxOption[];
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  /** Optional aria-label for the combobox button when no visible label is wired via aria-labelledby. */
  ariaLabel?: string;
}

/**
 * Phase 6.13.4 follow-up: replaces native `<select>` everywhere in admin-panel
 * because Chrome on Windows ignores `color-scheme: dark` for native dropdown
 * popups. Implements the WAI-ARIA 1.2 combobox pattern (button + listbox) with
 * full keyboard nav, typeahead, click-outside, and brand-matched glass styling.
 */
export function Combobox({
  value, onChange, options, placeholder, className, disabled, ariaLabel,
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const listboxRef = useRef<HTMLUListElement>(null);
  const baseId = useId();
  const listboxId = `${baseId}-listbox`;
  const typeaheadBufferRef = useRef('');
  const typeaheadTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const selected = options.find(o => o.value === value);

  // Sync activeIndex when opening to the currently selected option.
  useEffect(() => {
    if (!open) return;
    const i = options.findIndex(o => o.value === value);
    setActiveIndex(i >= 0 ? i : 0);
  }, [open, options, value]);

  // Click-outside closes.
  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (!buttonRef.current?.contains(e.target as Node) &&
          !listboxRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  // Keep the active option scrolled into view as keyboard nav moves it.
  // jsdom (vitest) lacks scrollIntoView, so guard the call.
  useEffect(() => {
    if (!open || activeIndex < 0) return;
    const node = listboxRef.current?.querySelector<HTMLLIElement>(`[data-index="${activeIndex}"]`);
    if (node && typeof node.scrollIntoView === 'function') {
      node.scrollIntoView({ block: 'nearest' });
    }
  }, [open, activeIndex]);

  const selectIndex = useCallback((i: number) => {
    if (i < 0 || i >= options.length) return;
    onChange(options[i].value);
    setOpen(false);
    buttonRef.current?.focus();
  }, [options, onChange]);

  const handleKey = (e: React.KeyboardEvent) => {
    if (disabled) return;
    if (!open) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        setOpen(true);
      }
      return;
    }
    switch (e.key) {
      case 'Escape':
        e.preventDefault();
        setOpen(false);
        buttonRef.current?.focus();
        return;
      case 'ArrowDown':
        e.preventDefault();
        setActiveIndex(i => Math.min(options.length - 1, i + 1));
        return;
      case 'ArrowUp':
        e.preventDefault();
        setActiveIndex(i => Math.max(0, i - 1));
        return;
      case 'Home':
        e.preventDefault();
        setActiveIndex(0);
        return;
      case 'End':
        e.preventDefault();
        setActiveIndex(options.length - 1);
        return;
      case 'Enter':
      case ' ':
        e.preventDefault();
        selectIndex(activeIndex);
        return;
      case 'Tab':
        // Tab lets focus leave naturally; close so the popover doesn't linger.
        setOpen(false);
        return;
    }
    // Typeahead — single printable char jumps to first option whose label
    // (case-insensitive) starts with the accumulated buffer. Buffer resets
    // after 600ms of inactivity, matching native <select> behavior.
    if (e.key.length === 1) {
      if (typeaheadTimerRef.current) clearTimeout(typeaheadTimerRef.current);
      typeaheadBufferRef.current += e.key.toLowerCase();
      const buf = typeaheadBufferRef.current;
      const idx = options.findIndex(o => o.label.toLowerCase().startsWith(buf));
      if (idx >= 0) setActiveIndex(idx);
      typeaheadTimerRef.current = setTimeout(() => { typeaheadBufferRef.current = ''; }, 600);
    }
  };

  const showPlaceholder = !selected;
  const labelText = selected?.label ?? placeholder ?? '';

  return (
    <div className={`relative ${className ?? ''}`}>
      <button
        ref={buttonRef}
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-activedescendant={open && activeIndex >= 0 ? `${baseId}-opt-${activeIndex}` : undefined}
        aria-label={ariaLabel}
        disabled={disabled}
        onClick={() => !disabled && setOpen(o => !o)}
        onKeyDown={handleKey}
        className={`input-dark w-full flex items-center justify-between text-left ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
      >
        <span className={showPlaceholder ? 'text-white/35 truncate' : 'truncate'}>{labelText || '\u00A0'}</span>
        <ChevronDown className={`w-3.5 h-3.5 ml-2 shrink-0 text-white/50 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      {open && (
        <ul
          ref={listboxRef}
          role="listbox"
          id={listboxId}
          tabIndex={-1}
          className="absolute left-0 right-0 z-50 mt-1 max-h-60 overflow-y-auto rounded-md border border-white/[0.08] bg-surface-900/95 backdrop-blur-xl shadow-[0_8px_24px_rgba(0,0,0,0.5)] py-1"
        >
          {options.map((o, i) => {
            const sel = o.value === value;
            const act = i === activeIndex;
            return (
              <li
                key={o.value}
                id={`${baseId}-opt-${i}`}
                role="option"
                aria-selected={sel}
                data-index={i}
                onMouseDown={(e) => { e.preventDefault(); selectIndex(i); }}
                onMouseEnter={() => setActiveIndex(i)}
                className={`px-3 py-2 text-xs font-mono cursor-pointer flex items-center justify-between gap-2 ${
                  act ? 'bg-accent/[0.12] text-accent' : 'text-white/80 hover:bg-white/[0.04]'
                }`}
              >
                <span className="truncate">{o.label}</span>
                {sel && <Check className="w-3.5 h-3.5 shrink-0" />}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
