const fallbackReturnUrl = '/builder';
const publicAuthPaths = new Set(['/signin', '/register']);

export function buildReturnUrlParam(pathname: string, search = ''): string {
  return `${pathname}${search}`;
}

export function readSafeReturnUrl(value: string | null): string {
  if (!value || !value.startsWith('/') || value.startsWith('//')) {
    return fallbackReturnUrl;
  }

  try {
    const parsed = new URL(value, 'https://outfit-planner.local');

    if (parsed.origin !== 'https://outfit-planner.local' || publicAuthPaths.has(parsed.pathname)) {
      return fallbackReturnUrl;
    }

    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return fallbackReturnUrl;
  }
}
