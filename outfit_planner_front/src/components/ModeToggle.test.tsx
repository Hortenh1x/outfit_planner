import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ModeToggle } from './ModeToggle';

describe('ModeToggle', () => {
  it('switches between clothes-only and on-person preview modes', async () => {
    const onChange = vi.fn();

    render(<ModeToggle mode="clothes" onChange={onChange} />);

    expect(screen.getByRole('button', { name: /clothes only/i })).toHaveAttribute('aria-pressed', 'true');
    await userEvent.click(screen.getByRole('button', { name: /on me/i }));

    expect(onChange).toHaveBeenCalledWith('person');
  });
});
