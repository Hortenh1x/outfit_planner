import { useRef } from 'react';
import type { PointerEvent as ReactPointerEvent } from 'react';
import { RotateCw } from 'lucide-react';

interface RotateControlProps {
  /** Absolute rotation in degrees, normalized to (-180, 180]. */
  value: number;
  onChange: (degrees: number) => void;
  disabled?: boolean;
}

const DEGREES_PER_PIXEL = 0.75;

/**
 * Press-and-hold the handle, then drag horizontally to spin the garment preview to any angle.
 * The parent applies `value` as the live preview transform; on save the absolute angle is sent
 * to the backend, which re-renders the cutout from its immutable base.
 */
export function RotateControl({ value, onChange, disabled = false }: RotateControlProps) {
  const drag = useRef<{ startX: number; startValue: number } | null>(null);

  function beginDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (disabled) {
      return;
    }
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = { startX: event.clientX, startValue: value };
  }

  function moveDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (!drag.current) {
      return;
    }
    const delta = (event.clientX - drag.current.startX) * DEGREES_PER_PIXEL;
    onChange(normalizeDegrees(drag.current.startValue + delta));
  }

  function endDrag(event: ReactPointerEvent<HTMLButtonElement>) {
    if (drag.current && event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    drag.current = null;
  }

  return (
    <div
      className="rotate-control"
      style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}
    >
      <button
        type="button"
        className="wardrobe-secondary-button rotate-control-handle"
        style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', cursor: 'ew-resize', touchAction: 'none' }}
        aria-label="Hold and drag to rotate the garment"
        disabled={disabled}
        onPointerDown={beginDrag}
        onPointerMove={moveDrag}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onLostPointerCapture={() => {
          drag.current = null;
        }}
      >
        <RotateCw aria-hidden size={15} />
        <span>Hold &amp; drag to rotate</span>
      </button>
      <output className="rotate-control-value" style={{ fontVariantNumeric: 'tabular-nums', minWidth: '3.5ch' }}>
        {Math.round(value)}&deg;
      </output>
      <button
        type="button"
        className="wardrobe-secondary-button"
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
