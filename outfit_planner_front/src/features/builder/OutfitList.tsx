import { Eye } from 'lucide-react';
import type { Outfit } from '../../types';

export function OutfitList({ outfits, onPick }: { outfits: Outfit[]; onPick: (outfit: Outfit) => void }) {
  if (outfits.length === 0) {
    return null;
  }

  return (
    <div className="saved-list">
      <h3>Saved outfits</h3>
      {outfits.map((outfit) => (
        <button type="button" key={outfit.id} onClick={() => onPick(outfit)}>
          <Eye size={15} />
          {outfit.name}
        </button>
      ))}
    </div>
  );
}
