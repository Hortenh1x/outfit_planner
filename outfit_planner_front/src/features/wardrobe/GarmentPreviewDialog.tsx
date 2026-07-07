import { useEffect } from 'react';
import { X } from 'lucide-react';
import type { GarmentItem } from '../../types';

interface GarmentPreviewDialogProps {
  garment: GarmentItem;
  onClose: () => void;
}

/**
 * View-only enlarged preview of a wardrobe garment, mirroring the account avatar preview: a dimmed
 * fixed overlay that closes on backdrop click, the close button, or Escape. No selection or editing
 * happens here — the selection checkmark lives on the minimized card.
 */
export function GarmentPreviewDialog({ garment, onClose }: GarmentPreviewDialogProps) {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <div
      className="wardrobe-preview"
      role="dialog"
      aria-modal="true"
      aria-label={`Preview ${garment.name}`}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="wardrobe-preview-frame">
        <button type="button" className="wardrobe-preview-close" aria-label="Close preview" onClick={onClose}>
          <X size={18} aria-hidden="true" />
        </button>
        <img src={garment.imageUrl || garment.thumbnailUrl} alt={garment.name} />
      </div>
    </div>
  );
}
