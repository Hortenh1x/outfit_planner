// Billing checkout/portal URLs come from our API as Stripe-hosted https links. Guard the
// redirect so a malformed, non-https, or javascript:/data: value can never turn into a
// script-executing or off-origin navigation, even if the API response is ever tampered
// with. Returns whether the navigation was allowed (used by tests).
export function redirectToCheckout(
  url: string,
  assign: (target: string) => void = (target) => window.location.assign(target)
): boolean {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return false;
  }

  if (parsed.protocol !== 'https:') {
    return false;
  }

  assign(url);
  return true;
}
