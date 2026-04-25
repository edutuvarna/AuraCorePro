import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Combobox, ComboboxOption } from '@/components/Combobox';

const OPTS: ComboboxOption[] = [
  { value: 'a', label: 'Apple' },
  { value: 'b', label: 'Banana' },
  { value: 'c', label: 'Cherry' },
];

describe('Combobox', () => {
  let onChange: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    onChange = vi.fn();
  });

  it('renders the selected option label in closed state', () => {
    render(<Combobox value="b" onChange={onChange} options={OPTS} />);
    expect(screen.getByRole('combobox')).toHaveTextContent('Banana');
    // Listbox not in DOM until opened.
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('renders the placeholder when value matches no option', () => {
    render(<Combobox value="" onChange={onChange} options={OPTS} placeholder="Pick fruit" />);
    expect(screen.getByRole('combobox')).toHaveTextContent('Pick fruit');
  });

  it('opens the listbox on click and closes on second click', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} />);
    const btn = screen.getByRole('combobox');
    fireEvent.click(btn);
    expect(screen.getByRole('listbox')).toBeTruthy();
    expect(btn).toHaveAttribute('aria-expanded', 'true');
    fireEvent.click(btn);
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('selects an option on click and closes the listbox', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} />);
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.mouseDown(screen.getByText('Cherry'));
    expect(onChange).toHaveBeenCalledWith('c');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('navigates with ArrowDown / ArrowUp and selects with Enter', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} />);
    const btn = screen.getByRole('combobox');
    fireEvent.keyDown(btn, { key: 'ArrowDown' }); // opens, activeIndex=0 (selected="a")
    fireEvent.keyDown(btn, { key: 'ArrowDown' }); // activeIndex=1
    fireEvent.keyDown(btn, { key: 'ArrowDown' }); // activeIndex=2
    fireEvent.keyDown(btn, { key: 'Enter' });
    expect(onChange).toHaveBeenCalledWith('c');
  });

  it('closes on Escape without changing the value', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} />);
    const btn = screen.getByRole('combobox');
    fireEvent.click(btn);
    expect(screen.getByRole('listbox')).toBeTruthy();
    fireEvent.keyDown(btn, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(onChange).not.toHaveBeenCalled();
  });

  it('typeahead jumps to the first option whose label starts with the typed letter', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} />);
    const btn = screen.getByRole('combobox');
    fireEvent.keyDown(btn, { key: 'ArrowDown' }); // open
    fireEvent.keyDown(btn, { key: 'c' });          // typeahead → Cherry
    fireEvent.keyDown(btn, { key: 'Enter' });
    expect(onChange).toHaveBeenCalledWith('c');
  });

  it('respects disabled — click does nothing', () => {
    render(<Combobox value="a" onChange={onChange} options={OPTS} disabled />);
    fireEvent.click(screen.getByRole('combobox'));
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('closes when clicking outside the combobox', async () => {
    render(
      <div>
        <Combobox value="a" onChange={onChange} options={OPTS} />
        <button>outside</button>
      </div>
    );
    fireEvent.click(screen.getByRole('combobox'));
    expect(screen.getByRole('listbox')).toBeTruthy();
    fireEvent.mouseDown(screen.getByText('outside'));
    await waitFor(() => {
      expect(screen.queryByRole('listbox')).toBeNull();
    });
  });
});
