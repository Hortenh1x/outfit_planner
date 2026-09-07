import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DemoNotice } from './DemoNotice';

const storageKey = 'outfit-planner-demo-notice-dismissed-v2';

describe('DemoNotice', () => {
  beforeEach(() => {
    localStorage.removeItem(storageKey);
  });

  afterEach(() => {
    cleanup();
    localStorage.removeItem(storageKey);
  });

  it('shows the demo message on a first visit', () => {
    render(<DemoNotice />);

    expect(
      screen.getByText('Demo: payments run in Stripe test mode, so nothing is ever charged')
    ).toBeInTheDocument();
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('reveals the info popover on click and keeps it open on repeat clicks', async () => {
    render(<DemoNotice />);

    await userEvent.click(screen.getByRole('button', { name: /about the test payment mode/i }));

    expect(
      screen.getByText(
        /Premium checkout is connected to Stripe in test mode: pay with the test card 4242 4242 4242 4242, any future expiry date and any CVC, and no real money is charged/
      )
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /about the test payment mode/i }));

    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('closes the info popover on an outside pointer press and on Escape', async () => {
    render(<DemoNotice />);
    const infoButton = screen.getByRole('button', { name: /about the test payment mode/i });

    await userEvent.click(infoButton);
    expect(screen.getByRole('note')).toBeInTheDocument();

    await userEvent.pointer({ keys: '[MouseLeft]', target: document.body });
    expect(screen.queryByRole('note')).not.toBeInTheDocument();

    await userEvent.click(infoButton);
    expect(screen.getByRole('note')).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('reveals the info popover on hover', async () => {
    render(<DemoNotice />);

    await userEvent.hover(screen.getByRole('button', { name: /about the test payment mode/i }));

    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('dismisses via the close button, persists, and stays hidden on remount', async () => {
    const first = render(<DemoNotice />);

    await userEvent.click(screen.getByRole('button', { name: /dismiss demo notice/i }));

    expect(
      screen.queryByText('Demo: payments run in Stripe test mode, so nothing is ever charged')
    ).not.toBeInTheDocument();
    expect(localStorage.getItem(storageKey)).toBe('1');

    first.unmount();
    render(<DemoNotice />);

    expect(
      screen.queryByText('Demo: payments run in Stripe test mode, so nothing is ever charged')
    ).not.toBeInTheDocument();
  });
});
