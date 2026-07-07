import { useRef, type CSSProperties, type ReactNode, type TouchEvent } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { CATEGORY_SIZE_TARGETS, computeRelativeSize } from './relativeSize';
import type { GarmentCategory } from '../../types';
import type { UserGender } from '../../api/client';

/**
 * The single composed-outfit renderer: a neutral gendered silhouette with garments fitted onto
 * fixed body zones. The Builder uses it interactively (swipe + arrows), while outfit cards and
 * the shared view render the exact same layout read-only — identical by construction, because
 * every zone/anchor/size constant lives here and scales with the `width` prop.
 *
 * Fit model: the silhouette is the base. Clothing widths come from the phase-1 relative-size
 * util (so real within-category proportions survive — a cropped top stays shorter than a
 * tunic), and each garment is anchored by its TOP edge to a body line (shoulders for
 * tops/dresses, waist for bottoms) rather than centered on a point, so garments "hang" on the
 * body and overlap each other the way real layers do. Shoes fit a box at the feet.
 */

// Scene coordinate space; every position/size below is in it and multiplied by
// (width / SCENE_WIDTH) at render time. The body is centered with side gutters for accessories.
const SCENE_WIDTH = 380;
const SCENE_HEIGHT = 720;
const CENTER_X = 190;

// Body anchor lines (scene units), tuned to the silhouettes drawn at the bottom of this file.
const SHOULDER_Y = 130; // top edge of tops/dresses hangs here (near the neck base)
const WAIST_Y = 298; // top edge of bottoms hangs here — high enough that the waistband sits at
// the silhouette waist and tucks under the top rather than riding down onto the thighs
const FEET_CENTER_Y = 662; // shoes are centered on the feet
const HAIR_CENTER = { x: CENTER_X, y: 70 };

// A worn top and bottom stack like a real outfit: the bottom hangs from just under the top's
// hem (not a fixed line), so the torso is always fully covered with no bare gap regardless of
// the top's length. 0.9 = the top drapes over the top ~10% of the bottom (waistband tucked in).
const TOP_BOTTOM_OVERLAP = 0.9;
// Fallback aspect (height/width) for an unmeasured garment, per width-anchored category, used to
// estimate a top's rendered height for the stack. Real garments use their measured cutout.
const FALLBACK_ASPECT: Partial<Record<GarmentCategory, number>> = { Top: 1.05, Dress: 1.7, Outerwear: 1.4 };

// Side-piece rails outside the body. Side pieces are fixed-size thumbnails (they are placed
// beside the figure, not worn on a zone), so they never collide with the clothing.
const SIDE_BOX = 68;
const ACCESSORY_CENTERS = [
  { x: 40, y: 196 },
  { x: 40, y: 338 },
  { x: 40, y: 480 }
];
const OUTERWEAR_CENTER = { x: 342, y: 222 };
const BAG_CENTER = { x: 342, y: 486 };

const HAIRSTYLE_WIDTH = 118;
const SWIPE_THRESHOLD_PX = 36;

type FigureSlot = 'top' | 'bottom' | 'shoes' | 'dress' | 'hairstyle';

export interface ComposedFigureGarment {
  id: string;
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  cutoutWidthPx?: number | null;
  cutoutHeightPx?: number | null;
}

export interface ComposedFigureHairstyle {
  name: string;
  assetUrl: string;
}

export interface ComposedFigurePieces {
  top?: ComposedFigureGarment;
  bottom?: ComposedFigureGarment;
  dress?: ComposedFigureGarment;
  shoes?: ComposedFigureGarment;
  outerwear?: ComposedFigureGarment;
  bag?: ComposedFigureGarment;
  accessories: ComposedFigureGarment[];
}

interface ComposedOutfitItemLike {
  garmentId: string;
  name: string;
  category: GarmentCategory;
  thumbnailUrl: string;
  cutoutWidthPx?: number | null;
  cutoutHeightPx?: number | null;
}

/**
 * Full-size figure width that still fits small phone viewports (side pieces included). Sized
 * once per mount, which is enough here (no live-resize reactivity needed).
 */
export function defaultFigureWidth(max = 340): number {
  if (typeof window === 'undefined') {
    return max;
  }

  return Math.min(max, Math.max(240, Math.floor(window.innerWidth - 56)));
}

/**
 * Maps saved outfit items onto figure slots so cards and the shared view reconstruct exactly
 * what the Builder composed (one garment per worn zone, up to three accessories).
 */
export function composedPiecesFromOutfitItems(items: ComposedOutfitItemLike[]): ComposedFigurePieces {
  const pieces: ComposedFigurePieces = { accessories: [] };
  for (const item of items) {
    const garment: ComposedFigureGarment = {
      id: item.garmentId,
      name: item.name,
      category: item.category,
      imageUrl: item.thumbnailUrl,
      cutoutWidthPx: item.cutoutWidthPx,
      cutoutHeightPx: item.cutoutHeightPx
    };
    switch (item.category) {
      case 'Top':
        pieces.top = garment;
        break;
      case 'Bottom':
        pieces.bottom = garment;
        break;
      case 'Dress':
        pieces.dress = garment;
        break;
      case 'Shoes':
        pieces.shoes = garment;
        break;
      case 'Outerwear':
        pieces.outerwear = garment;
        break;
      case 'Bag':
        pieces.bag = garment;
        break;
      case 'Accessory':
        if (pieces.accessories.length < ACCESSORY_CENTERS.length) {
          pieces.accessories = [...pieces.accessories, garment];
        }
        break;
    }
  }

  return pieces;
}

export interface ComposedFigureInteractions {
  cycleAvailability: Partial<Record<FigureSlot, boolean>>;
  emptyCarouselSlots: Partial<Record<'top' | 'bottom' | 'shoes', boolean>>;
  onCycle: (slot: FigureSlot, direction: 1 | -1) => void;
  onToggleHairstyle?: () => void;
  onRemove: (slot: 'dress' | 'outerwear' | 'bag') => void;
  onRemoveAccessory: (garmentId: string) => void;
}

export interface ComposedOutfitFigureProps {
  gender: UserGender;
  top?: ComposedFigureGarment | null;
  bottom?: ComposedFigureGarment | null;
  dress?: ComposedFigureGarment | null;
  shoes?: ComposedFigureGarment | null;
  outerwear?: ComposedFigureGarment | null;
  bag?: ComposedFigureGarment | null;
  accessories?: ComposedFigureGarment[];
  // Hairstyles are currently hidden from the product; the rendering path is kept for later use.
  hairstyle?: ComposedFigureHairstyle | null;
  hairstyleVisible?: boolean;
  width?: number;
  className?: string;
  interactive?: ComposedFigureInteractions;
}

export function ComposedOutfitFigure({
  gender,
  top,
  bottom,
  dress,
  shoes,
  outerwear,
  bag,
  accessories = [],
  hairstyle,
  hairstyleVisible = true,
  width = 340,
  className,
  interactive
}: ComposedOutfitFigureProps) {
  const scale = width / SCENE_WIDTH;
  const height = SCENE_HEIGHT * scale;

  // Stack the bottom right under the worn top's hem (scene units). With no top, the bottom
  // falls back to the fixed waist line.
  const topHeightScene = top ? CATEGORY_SIZE_TARGETS.Top.targetPx * garmentAspect(top, 'Top') : 0;
  const bottomAnchorY = top ? SHOULDER_Y + topHeightScene * TOP_BOTTOM_OVERLAP : WAIST_Y;

  return (
    <div
      className={`composed-figure${className ? ` ${className}` : ''}`}
      style={{ width, height }}
      data-gender={gender}
    >
      <SilhouetteSvg gender={gender} />

      {dress ? (
        <ClothingSlot slot="dress" garment={dress} scale={scale} zIndex={3} interactive={interactive} onTap={interactive ? () => interactive.onRemove('dress') : undefined} tapLabel={`Remove ${dress.name}`} />
      ) : (
        <>
          {bottom || interactive?.emptyCarouselSlots.bottom ? (
            <ClothingSlot slot="bottom" garment={bottom ?? null} scale={scale} zIndex={2} interactive={interactive} anchorYScene={bottomAnchorY} />
          ) : null}
          {top || interactive?.emptyCarouselSlots.top ? (
            <ClothingSlot slot="top" garment={top ?? null} scale={scale} zIndex={4} interactive={interactive} />
          ) : null}
        </>
      )}

      {shoes || interactive?.emptyCarouselSlots.shoes ? (
        <ClothingSlot slot="shoes" garment={shoes ?? null} scale={scale} zIndex={2} interactive={interactive} />
      ) : null}

      {hairstyle && hairstyleVisible ? (
        <ClothingSlot
          slot="hairstyle"
          hairstyle={hairstyle}
          scale={scale}
          zIndex={5}
          interactive={interactive}
          onTap={interactive?.onToggleHairstyle}
          tapLabel="Hide hairstyle"
        />
      ) : null}
      {hairstyle && !hairstyleVisible && interactive?.onToggleHairstyle ? (
        <button
          type="button"
          className="figure-hairstyle-ghost"
          style={{ left: HAIR_CENTER.x * scale, top: HAIR_CENTER.y * scale }}
          onClick={interactive.onToggleHairstyle}
          aria-label="Show hairstyle"
          title="Show hairstyle"
        />
      ) : null}

      {outerwear ? (
        <SidePiece garment={outerwear} center={OUTERWEAR_CENTER} scale={scale} onTap={interactive ? () => interactive.onRemove('outerwear') : undefined} />
      ) : null}
      {bag ? (
        <SidePiece garment={bag} center={BAG_CENTER} scale={scale} onTap={interactive ? () => interactive.onRemove('bag') : undefined} />
      ) : null}
      {accessories.slice(0, ACCESSORY_CENTERS.length).map((accessory, index) => (
        <SidePiece
          key={accessory.id}
          garment={accessory}
          center={ACCESSORY_CENTERS[index]}
          scale={scale}
          onTap={interactive ? () => interactive.onRemoveAccessory(accessory.id) : undefined}
        />
      ))}
    </div>
  );
}

function ClothingSlot({
  slot,
  garment,
  hairstyle,
  scale,
  zIndex,
  interactive,
  onTap,
  tapLabel,
  anchorYScene
}: {
  slot: FigureSlot;
  garment?: ComposedFigureGarment | null;
  hairstyle?: ComposedFigureHairstyle;
  scale: number;
  zIndex: number;
  interactive?: ComposedFigureInteractions;
  onTap?: () => void;
  tapLabel?: string;
  anchorYScene?: number;
}) {
  const touchStartX = useRef<number | null>(null);
  const canCycle = Boolean(interactive?.cycleAvailability[slot]);

  const handleTouchStart = (event: TouchEvent) => {
    touchStartX.current = event.touches[0]?.clientX ?? null;
  };

  const handleTouchEnd = (event: TouchEvent) => {
    if (touchStartX.current === null || !interactive) {
      return;
    }

    const deltaX = (event.changedTouches[0]?.clientX ?? touchStartX.current) - touchStartX.current;
    touchStartX.current = null;
    if (canCycle && Math.abs(deltaX) >= SWIPE_THRESHOLD_PX) {
      // Swiping left reveals the next item, right the previous one.
      interactive.onCycle(slot, deltaX < 0 ? 1 : -1);
    }
  };

  const size = hairstyle
    ? { width: HAIRSTYLE_WIDTH * scale, height: 'auto' as const }
    : garment
      ? garmentDisplaySize(garment, scale)
      : null;

  let content: ReactNode;
  if (hairstyle) {
    content = <img src={hairstyle.assetUrl} alt={hairstyle.name} style={{ width: HAIRSTYLE_WIDTH * scale, height: 'auto' }} draggable={false} />;
  } else if (garment && size) {
    content = <img src={garment.imageUrl} alt={garment.name} style={{ width: size.width, height: size.height }} draggable={false} />;
  } else {
    const target = slot === 'hairstyle' ? HAIRSTYLE_WIDTH : CATEGORY_SIZE_TARGETS[slotCategory(slot)].targetPx;
    content = <span className="figure-empty-slot" style={{ width: target * scale, height: target * 0.62 * scale }}>No {slot} yet</span>;
  }

  const body = onTap ? (
    <button type="button" className="figure-tap-target" onClick={onTap} aria-label={tapLabel}>
      {content}
    </button>
  ) : (
    content
  );

  // Arrows are absolutely-positioned overlays (not flow siblings) so a cycling slot's garment
  // stays centered on the body axis instead of being shoved sideways by the arrow widths.
  return (
    <div
      className="figure-slot"
      data-slot={slot}
      style={slotPlacementStyle(slot, scale, zIndex, anchorYScene)}
      onTouchStart={interactive ? handleTouchStart : undefined}
      onTouchEnd={interactive ? handleTouchEnd : undefined}
    >
      {body}
      {canCycle ? (
        <>
          <button type="button" className="figure-arrow figure-arrow-prev" aria-label={`Previous ${slot}`} onClick={() => interactive?.onCycle(slot, -1)}>
            <ChevronLeft size={16} />
          </button>
          <button type="button" className="figure-arrow figure-arrow-next" aria-label={`Next ${slot}`} onClick={() => interactive?.onCycle(slot, 1)}>
            <ChevronRight size={16} />
          </button>
        </>
      ) : null}
    </div>
  );
}

function SidePiece({
  garment,
  center,
  scale,
  onTap
}: {
  garment: ComposedFigureGarment;
  center: { x: number; y: number };
  scale: number;
  onTap?: () => void;
}) {
  const size = sidePieceSize(garment, scale);
  const image = <img src={garment.imageUrl} alt={garment.name} style={{ width: size.width, height: size.height }} draggable={false} />;

  return (
    <div className="figure-slot figure-side-piece" data-slot={garment.category.toLowerCase()} style={{ left: center.x * scale, top: center.y * scale, transform: 'translate(-50%, -50%)', zIndex: 1 }}>
      {onTap ? (
        <button type="button" className="figure-tap-target" onClick={onTap} aria-label={`Remove ${garment.name}`}>
          {image}
        </button>
      ) : (
        image
      )}
    </div>
  );
}

// Positions a clothing slot: tops/bottoms/dresses hang by their TOP edge from a body line
// (horizontally centered); shoes and the hairstyle are centered on their point. The bottom's
// line is dynamic (anchorYScene) so it stacks right under the worn top.
function slotPlacementStyle(slot: FigureSlot, scale: number, zIndex: number, anchorYScene?: number): CSSProperties {
  switch (slot) {
    case 'top':
    case 'dress':
      return { left: CENTER_X * scale, top: SHOULDER_Y * scale, transform: 'translateX(-50%)', zIndex };
    case 'bottom':
      return { left: CENTER_X * scale, top: (anchorYScene ?? WAIST_Y) * scale, transform: 'translateX(-50%)', zIndex };
    case 'shoes':
      return { left: CENTER_X * scale, top: FEET_CENTER_Y * scale, transform: 'translate(-50%, -50%)', zIndex };
    case 'hairstyle':
    default:
      return { left: HAIR_CENTER.x * scale, top: HAIR_CENTER.y * scale, transform: 'translate(-50%, -50%)', zIndex };
  }
}

// Height/width ratio of a garment's cutout (drives its rendered height and the outfit stack).
function garmentAspect(garment: ComposedFigureGarment, category: GarmentCategory): number {
  if (garment.cutoutHeightPx && garment.cutoutWidthPx) {
    return garment.cutoutHeightPx / garment.cutoutWidthPx;
  }
  return FALLBACK_ASPECT[category] ?? 1.1;
}

function garmentDisplaySize(garment: ComposedFigureGarment, scale: number): { width: number; height: number | 'auto' } {
  const target = CATEGORY_SIZE_TARGETS[garment.category];
  const relative = computeRelativeSize(garment);

  // Width-anchored clothing (top/bottom/dress/outerwear): pin the WIDTH and let the height be
  // the image's own aspect (`auto`). Pinning both dimensions would let `object-fit: contain`
  // letterbox a wide garment (e.g. shorts) into a narrower render than intended, which is what
  // made shorts look too small — width must be authoritative so a tee spans the shoulders and
  // shorts span the hips at their real proportions.
  if (target.axis === 'width') {
    return { width: (relative?.width ?? target.targetPx) * scale, height: 'auto' };
  }

  // Fixed-box categories (shoes): fit the box by the larger side, keeping both dimensions.
  if (relative) {
    return { width: relative.width * scale, height: relative.height * scale };
  }

  return { width: target.targetPx * scale, height: target.targetPx * scale };
}

// Side pieces are fit into a fixed square box by their larger side, preserving aspect.
function sidePieceSize(garment: ComposedFigureGarment, scale: number): { width: number; height: number } {
  const aspect = garment.cutoutHeightPx && garment.cutoutWidthPx ? garment.cutoutHeightPx / garment.cutoutWidthPx : 1;
  const box = SIDE_BOX * scale;
  return aspect >= 1 ? { width: box / aspect, height: box } : { width: box, height: box * aspect };
}

function slotCategory(slot: FigureSlot): GarmentCategory {
  switch (slot) {
    case 'top':
      return 'Top';
    case 'bottom':
      return 'Bottom';
    case 'shoes':
      return 'Shoes';
    case 'dress':
      return 'Dress';
    default:
      return 'Top';
  }
}

// Neutral flat mannequins drawn in the 380x720 scene space, composed from simple filled shapes
// (head, neck, tapered torso, pelvis, legs, feet). Slim, tapered proportions kept deliberately
// (a broader, less-tapered body reads as odd). The clothing anchors to the body lines —
// shoulders at y≈150, waist at y≈316, feet centered at y≈662 — and is sized a touch wider than
// the body in CATEGORY_SIZE_TARGETS so a tee drapes over the shoulders and shorts over the hips.
function SilhouetteSvg({ gender }: { gender: UserGender }) {
  return (
    <svg className="figure-silhouette" viewBox={`0 0 ${SCENE_WIDTH} ${SCENE_HEIGHT}`} xmlns="http://www.w3.org/2000/svg" aria-hidden="true" focusable="false">
      {gender === 'Male' ? (
        <g>
          <circle cx="190" cy="72" r="46" />
          <rect x="172" y="110" width="36" height="42" rx="12" />
          <path d="M108 158 Q106 147 118 146 L262 146 Q274 147 272 158 L250 318 Q248 330 236 330 L144 330 Q132 330 130 318 Z" />
          <path d="M132 322 L248 322 Q266 324 264 352 L260 398 Q258 410 246 410 L134 410 Q122 410 120 398 L116 352 Q114 324 132 322 Z" />
          <path d="M134 398 Q124 398 124 420 L128 650 Q128 668 146 668 L176 668 Q188 668 188 650 L188 420 Q188 400 178 398 Z" />
          <path d="M246 398 Q256 398 256 420 L252 650 Q252 668 234 668 L204 668 Q192 668 192 650 L192 420 Q192 400 202 398 Z" />
          <rect x="122" y="660" width="66" height="30" rx="10" />
          <rect x="192" y="660" width="66" height="30" rx="10" />
        </g>
      ) : (
        <g>
          <circle cx="190" cy="74" r="44" />
          <rect x="173" y="112" width="34" height="40" rx="14" />
          <path d="M115 158 Q112 149 123 148 L257 148 Q268 149 265 158 L246 315 Q244 326 232 326 L148 326 Q136 326 134 315 Z" />
          <path d="M138 318 L242 318 Q262 320 262 352 L258 396 Q256 410 244 410 L136 410 Q124 410 122 396 L118 352 Q118 320 138 318 Z" />
          <path d="M138 398 Q128 398 128 420 L132 648 Q132 664 150 664 L176 664 Q188 664 188 648 L188 420 Q188 400 178 398 Z" />
          <path d="M242 398 Q252 398 252 420 L248 648 Q248 664 230 664 L204 664 Q192 664 192 648 L192 420 Q192 400 202 398 Z" />
          <rect x="126" y="656" width="64" height="30" rx="12" />
          <rect x="190" y="656" width="64" height="30" rx="12" />
        </g>
      )}
    </svg>
  );
}
