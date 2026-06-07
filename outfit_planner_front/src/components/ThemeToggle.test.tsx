import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ThemeToggle } from './ThemeToggle';

describe('ThemeToggle', () => {
  it('switches between light and dark themes', async () => {
    const onChange = vi.fn();

    render(<ThemeToggle theme="light" onChange={onChange} />);

    expect(screen.getByRole('button', { name: /switch to dark theme/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /switch to dark theme/i }));

    expect(onChange).toHaveBeenCalledWith('dark');
  });
});
