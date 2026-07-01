import { useEffect, useRef, useState } from 'react';
import type { GarmentMetadataInput } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import { RotateControl } from '../../shared/ui/RotateControl';
import { fitScaleForRotation } from '../../shared/ui/rotationFit';
import type { Size } from '../../shared/ui/rotationFit';

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
  rotationDegrees: number;
}

export function GarmentEditor({ garment, isSaving, onCancel, onSave }: GarmentEditorProps) {
  const [form, setForm] = useState<GarmentEditorFormState>(() => formFromGarment(garment));
  const [isDirty, setIsDirty] = useState(false);
  const [isScrubbing, setIsScrubbing] = useState(false);
  const [source, setSource] = useState(() => sourceFromGarment(garment));
  const frameRef = useRef<HTMLDivElement>(null);
  const [imageNatural, setImageNatural] = useState<Size | null>(null);
  const [frameSize, setFrameSize] = useState<Size | null>(null);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame || typeof ResizeObserver === 'undefined') {
      return;
    }
    const observer = new ResizeObserver((entries) => {
      const rect = entries[0]?.contentRect;
      if (rect) {
        setFrameSize({ width: rect.width, height: rect.height });
      }
    });
    observer.observe(frame);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const nextSource = sourceFromGarment(garment);

    if (nextSource.garmentId !== source.garmentId) {
      setForm(nextSource.form);
      setSource(nextSource);
      setIsDirty(false);
      setImageNatural(null);
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

  // The stored cutout is already rendered at the garment's saved angle, so the live preview only
  // needs to rotate by the delta between the desired angle and the one already baked in.
  const previewAngle = form.rotationDegrees - Number(garment.rotationDegrees ?? 0);
  // Shrink the preview just enough that the rotated photo stays whole inside the frame instead of
  // having its corners clipped.
  const previewScale = fitScaleForRotation(previewAngle, imageNatural, frameSize);

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
          season: splitTokens(form.season),
          rotationDegrees: form.rotationDegrees
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
        <div className="wardrobe-editor-photo-frame" ref={frameRef}>
          <img
            className="wardrobe-editor-photo-preview"
            data-scrubbing={isScrubbing ? 'true' : undefined}
            src={form.thumbnailUrl || form.imageUrl}
            alt={`${garment.name} current photo`}
            style={{ transform: `rotate(${previewAngle}deg) scale(${previewScale})` }}
            onLoad={(event) => {
              const image = event.currentTarget;
              setImageNatural({ width: image.naturalWidth, height: image.naturalHeight });
            }}
          />
        </div>
        <p>Drag to straighten or rotate; the new angle is saved with the garment.</p>
        <RotateControl
          value={form.rotationDegrees}
          onChange={(degrees) => updateForm({ rotationDegrees: degrees })}
          onScrubbingChange={setIsScrubbing}
          disabled={isSaving}
        />
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
    tags: garment.tags.join(', '),
    rotationDegrees: Number(garment.rotationDegrees ?? 0)
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
