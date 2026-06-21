import { Grid2X2, List, Search, SlidersHorizontal, X } from 'lucide-react';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import type { WardrobeFilterState } from './wardrobeFilters';

const colorOptions = ['', 'black', 'cream', 'brown', 'blue', 'red', 'green'];
const seasonOptions = ['', 'spring', 'summer', 'fall', 'winter'];

interface WardrobeFiltersProps {
  filters: WardrobeFilterState;
  itemCount: number;
  onChange: (filters: WardrobeFilterState) => void;
  onReset: () => void;
}

export function WardrobeFilters({ filters, itemCount, onChange, onReset }: WardrobeFiltersProps) {
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
        <label>
          <span>Category filter</span>
          <select
            value={filters.category}
            onChange={(event) => onChange({ ...filters, category: event.target.value as WardrobeFilterState['category'] })}
          >
            <option value="All">All categories</option>
            {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
          </select>
        </label>
        <div className="wardrobe-view-buttons" aria-label="Catalog view">
          <button type="button" aria-label="Grid view" aria-pressed="true">
            <Grid2X2 size={16} aria-hidden="true" />
          </button>
          <button type="button" aria-label="List view" aria-pressed="false">
            <List size={16} aria-hidden="true" />
          </button>
        </div>
      </div>
      <div className="wardrobe-tab-row" role="tablist" aria-label="Garment categories">
        <button type="button" role="tab" aria-selected={filters.category === 'All'} onClick={() => onChange({ ...filters, category: 'All' })}>
          All
        </button>
        {GARMENT_CATEGORIES.map((category) => (
          <button
            key={category}
            type="button"
            role="tab"
            aria-selected={filters.category === category}
            onClick={() => onChange({ ...filters, category })}
          >
            {category}
          </button>
        ))}
        <span className="wardrobe-item-count">{itemCount} items</span>
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
        <label>
          <span>Tags</span>
          <input
            value={filters.tag}
            placeholder="silk, office, rain"
            onChange={(event) => onChange({ ...filters, tag: event.target.value })}
          />
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
