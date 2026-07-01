import { useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent, PointerEvent as ReactPointerEvent } from 'react';
import { RotateCw } from 'lucide-react';

interface RotateControlProps {
  /** Absolute rotation in degrees, normalized to (-180, 180]. */
  value: number;
  onChange: (degrees: number) => void;
  /**
   * Fires as a pointer scrub begins and ends, so the parent can drop its preview transition
   * while the drag is live (a tween fighting the drag reads as lag).
   */
  onScrubbingChange?: (scrubbing: boolean) => void;
  disabled?: boolean;
}

const DEGREES_PER_PIXEL = 0.75;
const FINE_STEP = 1;
const COARSE_STEP = 10;
const MIN_DEGREES = -180;
const MAX_DEGREES = 180;

/**
 * Press-and-hold the handle, then drag horizontally to spin the garment preview to any angle;
 * keyboard users focus the handle and nudge with the arrow keys (Shift for 10 degree steps).
 * The parent applies `value` as the live preview transform; on save the absolute angle is sent
 * to the backend, which re-renders the cutout from its immutable base.
 */
export function RotateControl({ value, onChange, onScrubbingChange, disabled = false }: RotateControlProps) {
  const drag = useRef<{ startX: number; startValue: number } | null>(null);
  const [scrubbing, setScrubbing] = useState(false);

  function setScrub(active: boolean) {
    setScrubbing(active);
    onScrubbingChange?.(active);
  }

  function beginDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (disabled) {
      return;
    }
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = { startX: event.clientX, startValue: value };
    setScrub(true);
  }

  function moveDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (!drag.current) {
      return;
    }
    const delta = (event.clientX - drag.current.startX) * DEGREES_PER_PIXEL;
    onChange(normalizeDegrees(drag.current.startValue + delta));
  }

  function endDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (!drag.current) {
      return;
    }
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    drag.current = null;
    setScrub(false);
  }

  function nudge(event: ReactKeyboardEvent<HTMLButtonElement>) {
    if (disabled) {
      return;
    }
    const step = event.shiftKey ? COARSE_STEP : FINE_STEP;
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowUp':
        next = value + step;
        break;
      case 'ArrowLeft':
      case 'ArrowDown':
        next = value - step;
        break;
      case 'Home':
        next = 0;
        break;
      default:
        return;
    }
    event.preventDefault();
    onChange(normalizeDegrees(next));
  }

  const rounded = Math.round(value);

  return (
    <div className="rotate-control">
      <button
        type="button"
        className="wardrobe-secondary-button rotate-control-handle"
        role="slider"
        aria-label="Rotate garment"
        aria-valuenow={rounded}
        aria-valuemin={MIN_DEGREES}
        aria-valuemax={MAX_DEGREES}
        aria-valuetext={`${rounded} degrees`}
        aria-orientation="horizontal"
        data-scrubbing={scrubbing ? 'true' : undefined}
        disabled={disabled}
        onPointerDown={beginDrag}
        onPointerMove={moveDrag}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onLostPointerCapture={() => {
          drag.current = null;
          setScrub(false);
        }}
        onKeyDown={nudge}
      >
        <RotateCw aria-hidden size={15} />
        <span>Rotate</span>
      </button>
      <output className="rotate-control-value">{rounded}&deg;</output>
      <button
        type="button"
        className="wardrobe-secondary-button rotate-control-reset"
        onClick={() => onChange(0)}
        disabled={disabled || Math.abs(value) < 0.5}
      >
        Reset
      </button>
    </div>
  );
}

/** Normalizes any angle to the minimal signed representation in (-180, 180]. */
export function normalizeDegrees(degrees: number): number {
  let wrapped = degrees % 360;
  if (wrapped > 180) {
    wrapped -= 360;
  } else if (wrapped <= -180) {
    wrapped += 360;
  }
  return wrapped;
}
