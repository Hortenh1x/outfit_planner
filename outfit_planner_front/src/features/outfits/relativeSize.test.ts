import { describe, expect, it } from 'vitest';
import { CATEGORY_SIZE_TARGETS, computeRelativeSize } from './relativeSize';
import type { GarmentCategory } from '../../types';

function garment(category: GarmentCategory, cutoutWidthPx: number | null, cutoutHeightPx: number | null) {
  return { category, cutoutWidthPx, cutoutHeightPx };
}

describe('computeRelativeSize', () => {
  it('renders the same garment identically regardless of shooting distance', () => {
    // The same shirt "shot" close up and far away: the alpha bounding box scales
    // proportionally, so only the aspect ratio survives — and the display size must match.
    const closeUp = computeRelativeSize(garment('Top', 400, 520));
    const farAway = computeRelativeSize(garment('Top', 200, 260));

    expect(closeUp).not.toBeNull();
    expect(farAway).toEqual(closeUp);
  });

  it('normalizes clothing by width so display height expresses real garment length', () => {
    const croppedTop = computeRelativeSize(garment('Top', 500, 400));
    const longTunic = computeRelativeSize(garment('Top', 500, 800));

    expect(croppedTop?.width).toBe(CATEGORY_SIZE_TARGETS.Top.targetPx);
    expect(longTunic?.width).toBe(CATEGORY_SIZE_TARGETS.Top.targetPx);
    expect(longTunic!.height).toBeGreaterThan(croppedTop!.height);
  });

  it('keeps cross-category proportions plausible: coat taller than shirt, shoes small', () => {
    const coat = computeRelativeSize(garment('Outerwear', 400, 640));
    const shirt = computeRelativeSize(garment('Top', 400, 480));
    const shoes = computeRelativeSize(garment('Shoes', 400, 200));

    expect(coat!.height).toBeGreaterThan(shirt!.height);
    expect(shirt!.height).toBeGreaterThan(shoes!.height);
    expect(Math.max(shoes!.width, shoes!.height)).toBe(CATEGORY_SIZE_TARGETS.Shoes.targetPx);
  });

  it('fits fixed-box categories by their larger dimension', () => {
    const tallBoot = computeRelativeSize(garment('Shoes', 300, 420));
    const wideSneaker = computeRelativeSize(garment('Shoes', 420, 210));

    expect(tallBoot?.height).toBe(CATEGORY_SIZE_TARGETS.Shoes.targetPx);
    expect(tallBoot!.width).toBeLessThan(CATEGORY_SIZE_TARGETS.Shoes.targetPx);
    expect(wideSneaker?.width).toBe(CATEGORY_SIZE_TARGETS.Shoes.targetPx);
    expect(wideSneaker!.height).toBeLessThan(CATEGORY_SIZE_TARGETS.Shoes.targetPx);
  });

  it('returns null for missing or invalid measurements so callers keep their fallback', () => {
    expect(computeRelativeSize(garment('Top', null, null))).toBeNull();
    expect(computeRelativeSize(garment('Top', 400, null))).toBeNull();
    expect(computeRelativeSize(garment('Top', 0, 300))).toBeNull();
    expect(computeRelativeSize(garment('Top', -100, 300))).toBeNull();
  });

  it('accepts an explicit category target override', () => {
    const custom = computeRelativeSize(garment('Top', 100, 200), { axis: 'width', targetPx: 50 });

    expect(custom).toEqual({ width: 50, height: 100 });
  });
});
