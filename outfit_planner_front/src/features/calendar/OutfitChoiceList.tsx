import { Check, Shirt } from 'lucide-react';
import type { Outfit } from '../../types';

export function OutfitChoiceList({
  outfits,
  selectedId,
  onSelect
}: {
  outfits: Outfit[];
  selectedId: string;
  onSelect: (outfitId: string) => void;
}) {
  if (outfits.length === 0) {
    return (
      <div className="choice-empty">
        <Shirt size={16} />
        <span>Save an outfit first</span>
      </div>
    );
  }

  return (
    <div className="choice-list" role="radiogroup" aria-label="Outfit">
      {outfits.map((outfit) => (
        <button
          type="button"
          key={outfit.id}
          role="radio"
          aria-checked={selectedId === outfit.id}
          className={selectedId === outfit.id ? 'selected' : ''}
          onClick={() => onSelect(outfit.id)}
        >
          <Shirt size={16} />
          <span>{outfit.name}</span>
          {selectedId === outfit.id ? <Check size={16} /> : null}
        </button>
      ))}
    </div>
  );
}
