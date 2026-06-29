import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RotateControl, normalizeDegrees } from './RotateControl';

afterEach(() => cleanup());

describe('normalizeDegrees', () => {
  it('keeps angles within the minimal signed range (-180, 180]', () => {
    expect(normalizeDegrees(0)).toBe(0);
    expect(normalizeDegrees(90)).toBe(90);
    expect(normalizeDegrees(180)).toBe(180);
    expect(normalizeDegrees(190)).toBe(-170);
    expect(normalizeDegrees(360)).toBe(0);
    expect(normalizeDegrees(450)).toBe(90);
    expect(normalizeDegrees(-200)).toBe(160);
  });
});

describe('RotateControl', () => {
  it('shows the current angle and resets to zero', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RotateControl value={42} onChange={onChange} />);

    expect(screen.getByText('42°')).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Reset' }));
    expect(onChange).toHaveBeenCalledWith(0);
  });

  it('disables reset when the garment is already upright', () => {
    render(<RotateControl value={0} onChange={() => undefined} />);
    const reset = screen.getByRole('button', { name: 'Reset' }) as HTMLButtonElement;
    expect(reset.disabled).toBe(true);
  });
});
