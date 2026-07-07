import { useEffect, useRef, useState } from 'react';
import type { FocusEvent as ReactFocusEvent, PointerEvent as ReactPointerEvent } from 'react';
import { AlertTriangle, Check, Loader2, Pencil, Trash2 } from 'lucide-react';
import type { GarmentItem } from '../../types';

interface GarmentCardProps {
  garment: GarmentItem;
  needsBetterPhoto?: boolean;
  pendingAction?: string;
  /** When set, a top-right checkmark toggles the garment in the quick-build selection. */
  selected?: boolean;
  onToggleSelect?: (garment: GarmentItem) => void;
  /** When set, tapping the photo opens the enlarged preview. */
  onEnlarge?: (garment: GarmentItem) => void;
  onDelete: (garment: GarmentItem) => void;
  onEdit: (garment: GarmentItem) => void;
}

const LONG_PRESS_MS = 450;
const MOVE_CANCEL_PX = 10;

/**
 * Image-first wardrobe card. The photo carries the card; edit/delete stay out of the way until the
 * user asks for them: hover on desktop, focus for keyboards, and press-and-hold on touch. The
 * actions remain in the accessibility tree at all times (hidden with opacity, not display), so
 * screen readers and keyboards can always reach them.
 */
export function GarmentCard({
  garment,
  needsBetterPhoto = false,
  pendingAction,
  selected = false,
  onToggleSelect,
  onEnlarge,
  onDelete,
  onEdit
}: GarmentCardProps) {
  const disabled = Boolean(pendingAction);
  const cardRef = useRef<HTMLElement>(null);
  const longPress = useRef<{ timer: number; x: number; y: number } | null>(null);
  // A completed long-press reveals edit/delete on touch; it must not also fire the enlarge tap.
  const longPressTriggered = useRef(false);
  const [revealed, setRevealed] = useState(false);

  function cancelLongPress() {
    if (longPress.current) {
      window.clearTimeout(longPress.current.timer);
      longPress.current = null;
    }
  }

  function handlePointerDown(event: ReactPointerEvent<HTMLElement>) {
    if (event.pointerType !== 'touch' || disabled) {
      return;
    }
    longPressTriggered.current = false;
    const { clientX: x, clientY: y } = event;
    const timer = window.setTimeout(() => {
      longPressTriggered.current = true;
      setRevealed(true);
    }, LONG_PRESS_MS);
    longPress.current = { timer, x, y };
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLElement>) {
    const press = longPress.current;
    if (!press) {
      return;
    }
    // A drag past the threshold is a scroll, not a long-press.
    if (Math.abs(event.clientX - press.x) > MOVE_CANCEL_PX || Math.abs(event.clientY - press.y) > MOVE_CANCEL_PX) {
      cancelLongPress();
    }
  }

  function handlePointerEnter(event: ReactPointerEvent<HTMLElement>) {
    if (event.pointerType !== 'touch' && !disabled) {
      setRevealed(true);
    }
  }

  function handlePointerLeave(event: ReactPointerEvent<HTMLElement>) {
    cancelLongPress();
    if (event.pointerType !== 'touch') {
      setRevealed(false);
    }
  }

  function handleBlur(event: ReactFocusEvent<HTMLElement>) {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setRevealed(false);
    }
  }

  // Dismiss a touch-revealed card when the next interaction lands outside it.
  useEffect(() => {
    if (!revealed) {
      return;
    }
    function handleDocumentPointerDown(event: PointerEvent) {
      if (!cardRef.current?.contains(event.target as Node)) {
        setRevealed(false);
      }
    }
    document.addEventListener('pointerdown', handleDocumentPointerDown);
    return () => document.removeEventListener('pointerdown', handleDocumentPointerDown);
  }, [revealed]);

  const isRemovingBackground =
    garment.backgroundRemovalStatus === 'Pending' || garment.backgroundRemovalStatus === 'Processing';

  return (
    <article
      ref={cardRef}
      className={garment.isArchived ? 'wardrobe-card archived' : 'wardrobe-card'}
      aria-busy={disabled}
      data-pending-action={pendingAction}
      data-revealed={revealed ? 'true' : undefined}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={cancelLongPress}
      onPointerCancel={cancelLongPress}
      onPointerEnter={handlePointerEnter}
      onPointerLeave={handlePointerLeave}
      onFocus={() => setRevealed(true)}
      onBlur={handleBlur}
    >
      <div className="wardrobe-card-image">
        {onEnlarge ? (
          <button
            type="button"
            className="wardrobe-card-enlarge"
            aria-label={`Enlarge ${garment.name}`}
            disabled={disabled}
            onClick={() => {
              if (longPressTriggered.current) {
                return;
              }
              onEnlarge(garment);
            }}
          >
            <img src={garment.thumbnailUrl || garment.imageUrl} alt={garment.name} />
          </button>
        ) : (
          <img src={garment.thumbnailUrl || garment.imageUrl} alt={garment.name} />
        )}
        {onToggleSelect ? (
          <button
            type="button"
            className="wardrobe-card-select"
            aria-pressed={selected}
            aria-label={selected ? `Deselect ${garment.name}` : `Select ${garment.name} for an outfit`}
            disabled={disabled}
            onPointerDown={(event) => event.stopPropagation()}
            onClick={(event) => {
              event.stopPropagation();
              onToggleSelect(garment);
            }}
          >
            <Check size={16} aria-hidden="true" />
          </button>
        ) : null}
        {isRemovingBackground ? (
          <div className="wardrobe-card-removing" role="status" aria-label={`Removing background for ${garment.name}`}>
            <Loader2 size={16} className="upload-queue-spinner" aria-hidden="true" />
            <span>Removing background…</span>
          </div>
        ) : null}
        {needsBetterPhoto || garment.backgroundRemovalStatus === 'Failed' ? (
          <div className="wardrobe-card-badges">
            {needsBetterPhoto ? (
              <span className="wardrobe-photo-warning" role="status" aria-label={`Needs better photo for ${garment.name}`}>
                <AlertTriangle size={14} aria-hidden="true" />
                Needs better photo?
              </span>
            ) : null}
            {garment.backgroundRemovalStatus === 'Failed' ? (
              <span
                className="wardrobe-photo-warning"
                role="status"
                aria-label={`Background removal failed for ${garment.name}`}
              >
                <AlertTriangle size={14} aria-hidden="true" />
                Background removal failed
              </span>
            ) : null}
          </div>
        ) : null}
        <div className="wardrobe-card-overlay" role="group" aria-label={`Actions for ${garment.name}`}>
          <button
            type="button"
            aria-label={`Edit ${garment.name}`}
            disabled={disabled}
            onClick={() => onEdit(garment)}
          >
            <Pencil size={16} aria-hidden="true" />
          </button>
          <button
            type="button"
            className="wardrobe-card-delete"
            aria-label={`Delete ${garment.name}`}
            disabled={disabled}
            onClick={() => onDelete(garment)}
          >
            <Trash2 size={16} aria-hidden="true" />
          </button>
        </div>
      </div>
    </article>
  );
}
