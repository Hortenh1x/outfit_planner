import type { ChangeEvent } from 'react';
import { GarmentCategoryIcon } from '../../shared/ui/GarmentCategoryControl';
import type { GarmentCategory, GarmentItem } from '../../types';

export function SlotPicker({
  title,
  category,
  garments,
  selectedId,
  selectedIds,
  onSelect,
  onQuickAdd,
  isQuickAdding
}: {
  title: string;
  category: GarmentCategory;
  garments: GarmentItem[];
  selectedId?: string;
  /** Multi-select highlight (e.g. accessories); takes precedence over selectedId when set. */
  selectedIds?: string[];
  onSelect: (id: string) => void;
  onQuickAdd: (event: ChangeEvent<HTMLInputElement>) => void;
  isQuickAdding: boolean;
}) {
  const lowerTitle = title.toLowerCase();
  const isSelected = (id: string) => (selectedIds ? selectedIds.includes(id) : selectedId === id);

  return (
    <div className="slot-picker">
      <h3>{title}</h3>
      {garments.map((garment) => (
        <button
          type="button"
          key={garment.id}
          className={isSelected(garment.id) ? 'garment-button selected' : 'garment-button'}
          onClick={() => onSelect(garment.id)}
        >
          <img src={garment.thumbnailUrl} alt="" />
          <span>{garment.name}</span>
        </button>
      ))}
      {garments.length === 0 ? (
        <label className="inline-empty" aria-disabled={isQuickAdding}>
          <GarmentCategoryIcon category={category} size={18} />
          <span>{isQuickAdding ? `Adding ${lowerTitle}` : `Add a ${lowerTitle} in Wardrobe`}</span>
          <input type="file" accept="image/png,image/jpeg,image/webp" disabled={isQuickAdding} onChange={onQuickAdd} data-category={category} />
        </label>
      ) : null}
    </div>
  );
}
