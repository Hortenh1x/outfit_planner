import { useEffect } from 'react';
import { Pencil, X } from 'lucide-react';
import { ComposedOutfitFigure, composedPiecesFromOutfitItems } from '../outfits/ComposedOutfitFigure';
import type { Outfit } from '../../types';

interface OutfitPreviewDialogProps {
  outfit: Outfit;
  onClose: () => void;
  onOpenInBuilder: (outfit: Outfit) => void;
}

/**
 * Enlarged view of a saved outfit, mirroring the wardrobe garment preview: a dimmed fixed overlay
 * that closes on backdrop click, the close button, or Escape. When the outfit has a generated
 * try-on preview it is shown full size; otherwise the composed figure is enlarged. "Open in
 * builder" loads the outfit into the Builder for editing, regeneration, or sharing.
 */
export function OutfitPreviewDialog({ outfit, onClose, onOpenInBuilder }: OutfitPreviewDialogProps) {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const pieces = composedPiecesFromOutfitItems(outfit.items);

  return (
    <div
      className="outfit-preview"
      role="dialog"
      aria-modal="true"
      aria-label={`Preview ${outfit.name}`}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="outfit-preview-frame">
        <button type="button" className="outfit-preview-close" aria-label="Close preview" onClick={onClose}>
          <X size={18} aria-hidden="true" />
        </button>
        <div className="outfit-preview-stage">
          {outfit.personPreviewUrl ? (
            <img className="outfit-preview-image" src={outfit.personPreviewUrl} alt={`${outfit.name} try-on preview`} />
          ) : (
            <ComposedOutfitFigure
              gender={outfit.silhouetteGender ?? 'Female'}
              top={pieces.top}
              bottom={pieces.bottom}
              dress={pieces.dress}
              shoes={pieces.shoes}
              outerwear={pieces.outerwear}
              bag={pieces.bag}
              accessories={pieces.accessories}
              width={enlargedFigureWidth()}
            />
          )}
        </div>
        <div className="outfit-preview-meta">
          <span className="outfit-preview-name">{outfit.name}</span>
          <button type="button" className="primary-action" onClick={() => onOpenInBuilder(outfit)}>
            <Pencil size={16} aria-hidden="true" />
            Open in builder
          </button>
        </div>
      </div>
    </div>
  );
}

// Sizes the enlarged composed figure so its full height (scene aspect 720/380) fits within ~68vh
// and ~80vw, clamped so it always reads clearly larger than the ~132px card miniature.
function enlargedFigureWidth(): number {
  if (typeof window === 'undefined') {
    return 300;
  }

  const figureAspect = 720 / 380;
  const byHeight = Math.floor((window.innerHeight * 0.68) / figureAspect);
  const byWidth = Math.floor(window.innerWidth * 0.8);
  return Math.max(220, Math.min(340, byHeight, byWidth));
}
