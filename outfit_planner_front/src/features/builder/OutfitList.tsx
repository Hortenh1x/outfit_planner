import { useState } from 'react';
import { Eye } from 'lucide-react';
import { ComposedOutfitFigure, composedPiecesFromOutfitItems } from '../outfits/ComposedOutfitFigure';
import { OutfitPreviewDialog } from './OutfitPreviewDialog';
import type { Outfit } from '../../types';

// Saved-outfit cards show the generated try-on preview when the outfit has one, otherwise the same
// composed figure as the Builder canvas (read-only, just smaller). Clicking a card enlarges it in a
// view-only dialog (mirroring the wardrobe garment preview); "Open in builder" there loads the
// outfit for editing, regeneration, or sharing.
export function OutfitList({ outfits, onPick }: { outfits: Outfit[]; onPick: (outfit: Outfit) => void }) {
  const [previewOutfit, setPreviewOutfit] = useState<Outfit | null>(null);

  if (outfits.length === 0) {
    return null;
  }

  return (
    <div className="saved-list">
      <h3>Saved outfits</h3>
      <div className="saved-outfit-cards">
        {outfits.map((outfit) => {
          const pieces = composedPiecesFromOutfitItems(outfit.items);
          return (
            <button
              type="button"
              key={outfit.id}
              className="saved-outfit-card"
              aria-label={`Enlarge ${outfit.name}`}
              onClick={() => setPreviewOutfit(outfit)}
            >
              {outfit.personPreviewUrl ? (
                <span className="saved-outfit-card-preview">
                  <img src={outfit.personPreviewUrl} alt="" />
                </span>
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
                  width={132}
                />
              )}
              <span className="saved-outfit-name">
                <Eye size={14} />
                {outfit.name}
              </span>
            </button>
          );
        })}
      </div>
      {previewOutfit ? (
        <OutfitPreviewDialog
          outfit={previewOutfit}
          onClose={() => setPreviewOutfit(null)}
          onOpenInBuilder={(outfit) => {
            setPreviewOutfit(null);
            onPick(outfit);
          }}
        />
      ) : null}
    </div>
  );
}
