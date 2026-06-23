import { useMemo, useState, type FocusEvent } from 'react';
import { Grid2X2, List, Search, SlidersHorizontal, X } from 'lucide-react';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import type { WardrobeFilterState } from './wardrobeFilters';

const colorOptions = ['', 'black', 'cream', 'brown', 'blue', 'red', 'green'];
const seasonOptions = ['', 'spring', 'summer', 'fall', 'winter'];

export type WardrobeViewMode = 'grid' | 'list';

interface WardrobeFiltersProps {
  existingTags: string[];
  filters: WardrobeFilterState;
  itemCount: number;
  viewMode: WardrobeViewMode;
  onChange: (filters: WardrobeFilterState) => void;
  onReset: () => void;
  onViewModeChange: (viewMode: WardrobeViewMode) => void;
}

export function WardrobeFilters({
  existingTags,
  filters,
  itemCount,
  viewMode,
  onChange,
  onReset,
  onViewModeChange
}: WardrobeFiltersProps) {
  const [isTagMenuOpen, setIsTagMenuOpen] = useState(false);
  const normalizedTagSearch = filters.tag.trim().toLowerCase();
  const visibleTagSuggestions = useMemo(
    () => existingTags.filter(
      (tag) => normalizedTagSearch.length === 0 || tag.toLowerCase().includes(normalizedTagSearch)
    ),
    [existingTags, normalizedTagSearch]
  );
  const isTagListVisible = isTagMenuOpen && visibleTagSuggestions.length > 0;

  function closeTagMenuOnBlur(event: FocusEvent<HTMLLabelElement>) {
    const nextFocusedElement = event.relatedTarget;

    if (!nextFocusedElement || !event.currentTarget.contains(nextFocusedElement as Node)) {
      setIsTagMenuOpen(false);
    }
  }

  return (
    <section className="wardrobe-controls" aria-label="Wardrobe filters">
      <div className="wardrobe-search-row">
        <label className="wardrobe-search">
          <Search size={16} aria-hidden="true" />
          <span>Search wardrobe</span>
          <input
            value={filters.q}
            placeholder="Search wardrobe"
            onChange={(event) => onChange({ ...filters, q: event.target.value })}
          />
        </label>
        <div className="wardrobe-control-meta">
          <span className="wardrobe-item-count">{itemCount} {itemCount === 1 ? 'item' : 'items'}</span>
          <div className="wardrobe-view-buttons" aria-label="Catalog view">
            <button type="button" aria-label="Grid view" aria-pressed={viewMode === 'grid'} onClick={() => onViewModeChange('grid')}>
              <Grid2X2 size={16} aria-hidden="true" />
            </button>
            <button type="button" aria-label="List view" aria-pressed={viewMode === 'list'} onClick={() => onViewModeChange('list')}>
              <List size={16} aria-hidden="true" />
            </button>
          </div>
        </div>
      </div>
      <div className="wardrobe-tab-row" aria-label="Garment categories">
        <button type="button" aria-pressed={filters.category === 'All'} onClick={() => onChange({ ...filters, category: 'All' })}>
          All
        </button>
        {GARMENT_CATEGORIES.map((category) => (
          <button
            key={category}
            type="button"
            aria-pressed={filters.category === category}
            onClick={() => onChange({ ...filters, category })}
          >
            {category}
          </button>
        ))}
      </div>
      <div className="wardrobe-filter-row">
        <SlidersHorizontal size={16} aria-hidden="true" />
        <label>
          <span>Color</span>
          <select value={filters.color} onChange={(event) => onChange({ ...filters, color: event.target.value })}>
            {colorOptions.map((color) => <option key={color || 'any'} value={color}>{color || 'Any color'}</option>)}
          </select>
        </label>
        <label>
          <span>Season</span>
          <select value={filters.season} onChange={(event) => onChange({ ...filters, season: event.target.value })}>
            {seasonOptions.map((season) => <option key={season || 'any'} value={season}>{season || 'Any season'}</option>)}
          </select>
        </label>
        <label className="wardrobe-tag-combobox" onBlur={closeTagMenuOnBlur}>
          <span>Tags</span>
          <input
            aria-autocomplete="list"
            aria-controls="wardrobe-tag-suggestions"
            aria-expanded={isTagListVisible}
            role="combobox"
            value={filters.tag}
            placeholder="silk, office, rain"
            onChange={(event) => {
              onChange({ ...filters, tag: event.target.value });
              setIsTagMenuOpen(true);
            }}
            onFocus={() => setIsTagMenuOpen(true)}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setIsTagMenuOpen(false);
              }
            }}
          />
          {isTagListVisible ? (
            <div className="wardrobe-tag-menu" id="wardrobe-tag-suggestions" role="listbox" aria-label="Tag suggestions">
              {visibleTagSuggestions.map((tag) => (
                <button
                  key={tag}
                  type="button"
                  role="option"
                  aria-selected={filters.tag === tag}
                  onMouseDown={(event) => {
                    event.preventDefault();
                    onChange({ ...filters, tag });
                    setIsTagMenuOpen(false);
                  }}
                >
                  {tag}
                </button>
              ))}
            </div>
          ) : null}
        </label>
        <label className="wardrobe-check">
          <input
            type="checkbox"
            checked={filters.favorite}
            onChange={(event) => onChange({ ...filters, favorite: event.target.checked })}
          />
          Favorites
        </label>
        <label className="wardrobe-check">
          <input
            type="checkbox"
            checked={filters.archived}
            onChange={(event) => onChange({ ...filters, archived: event.target.checked })}
          />
          Archived
        </label>
        <label>
          <span>Sort</span>
          <select value={filters.sort} onChange={(event) => onChange({ ...filters, sort: event.target.value as WardrobeFilterState['sort'] })}>
            <option value="recent">Recent</option>
            <option value="oldest">Oldest</option>
            <option value="name">Name</option>
            <option value="category">Category</option>
          </select>
        </label>
        <button type="button" className="wardrobe-ghost-button" onClick={onReset}>
          <X size={15} aria-hidden="true" />
          Reset
        </button>
      </div>
    </section>
  );
}
