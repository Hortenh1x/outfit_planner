import { useEffect, useState } from 'react';
import type { GarmentMetadataInput } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';

export type GarmentEditorSaveInput = {
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  thumbnailUrl?: string;
  tags: string[];
} & GarmentMetadataInput;

interface GarmentEditorProps {
  garment: GarmentItem;
  isSaving: boolean;
  onCancel: () => void;
  onSave: (garmentId: string, input: GarmentEditorSaveInput) => void;
}

interface GarmentEditorFormState {
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  thumbnailUrl: string;
  primaryColor: string;
  season: string;
  tags: string;
}

export function GarmentEditor({ garment, isSaving, onCancel, onSave }: GarmentEditorProps) {
  const [form, setForm] = useState<GarmentEditorFormState>(() => formFromGarment(garment));
  const [isDirty, setIsDirty] = useState(false);
  const [source, setSource] = useState(() => sourceFromGarment(garment));

  useEffect(() => {
    const nextSource = sourceFromGarment(garment);

    if (nextSource.garmentId !== source.garmentId) {
      setForm(nextSource.form);
      setSource(nextSource);
      setIsDirty(false);
      return;
    }

    if (nextSource.signature !== source.signature) {
      setSource(nextSource);
      if (!isDirty) {
        setForm(nextSource.form);
      }
    }
  }, [garment, isDirty, source]);

  function updateForm(updates: Partial<GarmentEditorFormState>) {
    setIsDirty(true);
    setForm((current) => ({ ...current, ...updates }));
  }

  return (
    <form
      className="wardrobe-rail-form"
      aria-label={`Edit ${garment.name}`}
      onSubmit={(event) => {
        event.preventDefault();
        onSave(garment.id, {
          name: form.name.trim(),
          category: form.category,
          imageUrl: form.imageUrl.trim(),
          thumbnailUrl: form.thumbnailUrl.trim() || undefined,
          tags: splitTokens(form.tags),
          primaryColor: form.primaryColor.trim() || null,
          season: splitTokens(form.season)
        });
      }}
    >
      <div className="wardrobe-rail-heading">
        <span>Edit garment</span>
        <h2>{garment.name}</h2>
      </div>
      <label>
        <span>Name</span>
        <input
          value={form.name}
          onChange={(event) => updateForm({ name: event.target.value })}
          required
          disabled={isSaving}
        />
      </label>
      <label>
        <span>Type</span>
        <select
          value={form.category}
          onChange={(event) => updateForm({ category: event.target.value as GarmentCategory })}
          disabled={isSaving}
        >
          {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
        </select>
      </label>
      <div className="wardrobe-editor-photo" aria-label={`Current photo for ${garment.name}`}>
        <img src={form.thumbnailUrl || form.imageUrl} alt={`${garment.name} current photo`} />
        <p>Current photo is preserved.</p>
        <p>Photo replacement will use the upload or re-add flow later.</p>
      </div>
      <label>
        <span>Color</span>
        <input
          value={form.primaryColor}
          onChange={(event) => updateForm({ primaryColor: event.target.value })}
          disabled={isSaving}
        />
      </label>
      <label>
        <span>Season</span>
        <input
          value={form.season}
          onChange={(event) => updateForm({ season: event.target.value })}
          disabled={isSaving}
        />
      </label>
      <label>
        <span>Tags</span>
        <input
          value={form.tags}
          onChange={(event) => updateForm({ tags: event.target.value })}
          disabled={isSaving}
        />
      </label>
      <button type="submit" className="wardrobe-primary-button" disabled={isSaving}>
        {isSaving ? 'Saving' : 'Save changes'}
      </button>
      <button type="button" className="wardrobe-secondary-button" onClick={onCancel} disabled={isSaving}>
        Cancel
      </button>
    </form>
  );
}

function formFromGarment(garment: GarmentItem): GarmentEditorFormState {
  return {
    name: garment.name,
    category: garment.category,
    imageUrl: garment.imageUrl,
    thumbnailUrl: garment.thumbnailUrl ?? '',
    primaryColor: garment.primaryColor ?? '',
    season: (garment.season ?? []).join(', '),
    tags: garment.tags.join(', ')
  };
}

function sourceFromGarment(garment: GarmentItem): {
  garmentId: string;
  form: GarmentEditorFormState;
  signature: string;
} {
  const form = formFromGarment(garment);
  return {
    garmentId: garment.id,
    form,
    signature: JSON.stringify(form)
  };
}

function splitTokens(value: string): string[] {
  return value.split(',').map((token) => token.trim()).filter(Boolean);
}
