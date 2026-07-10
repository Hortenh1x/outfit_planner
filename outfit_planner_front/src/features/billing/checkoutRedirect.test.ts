import { describe, expect, it, vi } from 'vitest';
import { redirectToCheckout } from './checkoutRedirect';

describe('redirectToCheckout', () => {
  it('navigates to a valid https url', () => {
    const assign = vi.fn();
    expect(redirectToCheckout('https://checkout.stripe.com/c/pay/abc', assign)).toBe(true);
    expect(assign).toHaveBeenCalledWith('https://checkout.stripe.com/c/pay/abc');
  });

  it('refuses non-https schemes (javascript:, http:, data:)', () => {
    const assign = vi.fn();
    for (const bad of ['javascript:alert(1)', 'http://stripe.com', 'data:text/html,<script>1</script>']) {
      expect(redirectToCheckout(bad, assign)).toBe(false);
    }
    expect(assign).not.toHaveBeenCalled();
  });

  it('refuses a malformed url', () => {
    const assign = vi.fn();
    expect(redirectToCheckout('not a url', assign)).toBe(false);
    expect(assign).not.toHaveBeenCalled();
  });
});
