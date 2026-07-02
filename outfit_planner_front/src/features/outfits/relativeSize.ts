import type { GarmentCategory } from '../../types';

export type RelativeSizeAxis = 'width' | 'maxDimension';

export interface CategorySizeTarget {
  /**
   * Normalization anchor. 'width' pins the display width to targetPx (shoulders/waist act as
   * the anchor, so display height expresses the garment's real length). 'maxDimension' fits the
   * garment into a targetPx box by its larger side (compact items shown at a fixed footprint).
   */
  axis: RelativeSizeAxis;
  targetPx: number;
}

/**
 * The single place that encodes cross-category proportions. Canonical display sizes are chosen
 * so a coat renders taller than a shirt, a dress reads long, and shoes/accessories stay small.
 */
export const CATEGORY_SIZE_TARGETS: Record<GarmentCategory, CategorySizeTarget> = {
  Outerwear: { axis: 'width', targetPx: 200 },
  Dress: { axis: 'width', targetPx: 185 },
  Top: { axis: 'width', targetPx: 180 },
  Bottom: { axis: 'width', targetPx: 160 },
  Bag: { axis: 'maxDimension', targetPx: 140 },
  Shoes: { axis: 'maxDimension', targetPx: 120 },
  Accessory: { axis: 'maxDimension', targetPx: 90 }
};

export interface RelativeDisplaySize {
  width: number;
  height: number;
}

export interface MeasuredGarment {
  category: GarmentCategory;
  cutoutWidthPx?: number | null;
  cutoutHeightPx?: number | null;
}

/**
 * Display size (px) for a garment cutout at its category's canonical scale. Only the cutout's
 * height/width ratio is used — it is invariant to how close the garment was photographed — so
 * the same item shot from any distance renders at the same relative size. Returns null when the
 * garment has no usable measurement; callers keep their existing fallback layout.
 */
export function computeRelativeSize(
  garment: MeasuredGarment,
  target: CategorySizeTarget = CATEGORY_SIZE_TARGETS[garment.category]
): RelativeDisplaySize | null {
  if (!target || !isPositive(garment.cutoutWidthPx) || !isPositive(garment.cutoutHeightPx)) {
    return null;
  }

  const aspect = garment.cutoutHeightPx / garment.cutoutWidthPx;
  if (target.axis === 'width') {
    return { width: target.targetPx, height: target.targetPx * aspect };
  }

  return aspect >= 1
    ? { width: target.targetPx / aspect, height: target.targetPx }
    : { width: target.targetPx, height: target.targetPx * aspect };
}

function isPositive(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0;
}
