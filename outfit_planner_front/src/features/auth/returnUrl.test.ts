import { describe, expect, it } from 'vitest';
import { buildReturnUrlParam, readSafeReturnUrl } from './returnUrl';

describe('auth return URLs', () => {
  it('builds a returnUrl from current path and search', () => {
    expect(buildReturnUrlParam('/builder', '?tab=tryon')).toBe('/builder?tab=tryon');
  });

  it('falls back when returnUrl is missing unsafe or malformed', () => {
    expect(readSafeReturnUrl(null)).toBe('/builder');
    expect(readSafeReturnUrl('https://evil.test/builder')).toBe('/builder');
    expect(readSafeReturnUrl('//evil.test/builder')).toBe('/builder');
    expect(readSafeReturnUrl('builder')).toBe('/builder');
    expect(readSafeReturnUrl('/signin')).toBe('/builder');
    expect(readSafeReturnUrl('/register')).toBe('/builder');
  });

  it('allows internal app return URLs', () => {
    expect(readSafeReturnUrl('/builder?tab=tryon')).toBe('/builder?tab=tryon');
    expect(readSafeReturnUrl('/wardrobe#summer')).toBe('/wardrobe#summer');
  });
});
