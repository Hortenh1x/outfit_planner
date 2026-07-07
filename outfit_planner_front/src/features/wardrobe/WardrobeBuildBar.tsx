import { Layers3, X } from 'lucide-react';

interface WardrobeBuildBarProps {
  count: number;
  onClear: () => void;
  onBuild: () => void;
}

/**
 * Floating action bar shown while garments are selected for a quick build. Mirrors the media-picker
 * pattern: it appears only when at least one item is picked and carries the set into the Builder.
 */
export function WardrobeBuildBar({ count, onClear, onBuild }: WardrobeBuildBarProps) {
  if (count === 0) {
    return null;
  }

  return (
    <div className="wardrobe-build-bar" role="region" aria-label="Outfit selection">
      <span className="wardrobe-build-count" aria-live="polite">
        {count} selected
      </span>
      <div className="wardrobe-build-actions">
        <button type="button" className="wardrobe-build-clear" onClick={onClear}>
          <X size={16} aria-hidden="true" />
          Clear
        </button>
        <button type="button" className="wardrobe-build-go" onClick={onBuild}>
          <Layers3 size={16} aria-hidden="true" />
          Build
        </button>
      </div>
    </div>
  );
}
