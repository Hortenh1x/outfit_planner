import { Footprints, Layers3, Shirt } from 'lucide-react';
import { GARMENT_CATEGORIES } from '../../features/outfits/outfitUtils';
import type { GarmentCategory } from '../../types';

export function CategorySegmentedControl({
  value,
  onChange
}: {
  value: GarmentCategory;
  onChange: (category: GarmentCategory) => void;
}) {
  return (
    <fieldset className="segmented-field">
      <legend>Type</legend>
      <div className="choice-list" role="radiogroup" aria-label="Garment type">
        {GARMENT_CATEGORIES.map((category) => (
          <button
            type="button"
            key={category}
            className={value === category ? 'selected' : ''}
            role="radio"
            aria-checked={value === category}
            onPointerDown={() => onChange(category)}
            onClick={() => onChange(category)}
          >
            <GarmentCategoryIcon category={category} size={16} />
            <span>{category}</span>
          </button>
        ))}
      </div>
    </fieldset>
  );
}

export function GarmentCategoryIcon({ category, size = 16 }: { category: GarmentCategory; size?: number }) {
  if (category === 'Top') {
    return <Shirt size={size} />;
  }

  if (category === 'Bottom') {
    return <BottomsIcon size={size} />;
  }

  if (category === 'Shoes') {
    return <Footprints size={size} />;
  }

  return <Layers3 size={size} />;
}

function BottomsIcon({ size = 16 }: { size?: number }) {
  return (
    <svg
      aria-hidden="true"
      fill="none"
      height={size}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      viewBox="0 0 24 24"
      width={size}
    >
      <path d="M8 5h8l1 4H7l1-4Z" />
      <path d="M7 9 5 20h14L17 9" />
      <path d="M10 9 9 20" />
      <path d="M14 9l1 11" />
    </svg>
  );
}
