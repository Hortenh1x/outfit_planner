import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DemoNotice } from './DemoNotice';

const storageKey = 'outfit-planner-demo-notice-dismissed';

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
      screen.getByText('This is only a demo application: AI features available on request only')
    ).toBeInTheDocument();
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('reveals the info popover on click and keeps it open on repeat clicks', async () => {
    render(<DemoNotice />);

    await userEvent.click(screen.getByRole('button', { name: /why ai features are limited/i }));

    expect(
      screen.getByText(
        /Due to a lack of resources AI features cannot be supported with the power they need all the time, so if you want to see how this app really operates, feel free to contact me/
      )
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /why ai features are limited/i }));

    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('closes the info popover on an outside pointer press and on Escape', async () => {
    render(<DemoNotice />);
    const infoButton = screen.getByRole('button', { name: /why ai features are limited/i });

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

    await userEvent.hover(screen.getByRole('button', { name: /why ai features are limited/i }));

    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('dismisses via the close button, persists, and stays hidden on remount', async () => {
    const first = render(<DemoNotice />);

    await userEvent.click(screen.getByRole('button', { name: /dismiss demo notice/i }));

    expect(
      screen.queryByText('This is only a demo application: AI features available on request only')
    ).not.toBeInTheDocument();
    expect(localStorage.getItem(storageKey)).toBe('1');

    first.unmount();
    render(<DemoNotice />);

    expect(
      screen.queryByText('This is only a demo application: AI features available on request only')
    ).not.toBeInTheDocument();
  });
});
