import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { LegalPage } from './LegalPage';

describe('LegalPage', () => {
  afterEach(cleanup);

  it('renders the legal information sections', () => {
    render(
      <MemoryRouter>
        <LegalPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /legal & service information/i })).toBeInTheDocument();
    for (const section of [
      /service provider & contact/i,
      /demo service/i,
      /accounts & your data/i,
      /ai processing/i,
      /payments, subscriptions & credits/i,
      /third-party services/i,
      /content responsibility/i
    ]) {
      expect(screen.getByRole('heading', { name: section })).toBeInTheDocument();
    }
    expect(screen.getByRole('link', { name: /dmytro\.bolibok@gmail\.com/i })).toHaveAttribute(
      'href',
      'mailto:dmytro.bolibok@gmail.com'
    );
  });
});
