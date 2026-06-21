import type { DragEvent } from 'react';
import { Camera, CloudUpload, Plus } from 'lucide-react';
import type { GarmentCategory } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import { UploadQueue } from './UploadQueue';
import { cleanPhotoChecklist, type UploadQueueItem } from './wardrobeUpload';

type UploadQueueItemUpdates = Partial<Pick<UploadQueueItem, 'name' | 'category' | 'tags' | 'primaryColor' | 'season'>>;

export interface WardrobeUploadDefaults {
  category: GarmentCategory;
  color: string;
  season: string[];
  tags: string[];
}

interface WardrobeUploadPanelProps {
  queue: UploadQueueItem[];
  isUploading: boolean;
  defaults: WardrobeUploadDefaults;
  onAcceptTag: (itemId: string, tag: string) => void;
  onAddFiles: (files: File[]) => void;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onDefaultsChange: (defaults: WardrobeUploadDefaults) => void;
  onRemoveItem: (itemId: string) => void;
  onSubmitAll: () => void;
}

export function WardrobeUploadPanel({
  queue,
  isUploading,
  defaults,
  onAcceptTag,
  onAddFiles,
  onChangeItem,
  onDefaultsChange,
  onRemoveItem,
  onSubmitAll
}: WardrobeUploadPanelProps) {
  const submitDisabled = isUploading || queue.every((item) => item.status === 'invalid');

  function addInputFiles(fileList: FileList | null): boolean {
    if (isUploading) {
      return false;
    }

    onAddFiles(Array.from(fileList ?? []));
    return true;
  }

  function handleDragOver(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
  }

  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
    if (isUploading) {
      return;
    }

    onAddFiles(Array.from(event.dataTransfer.files));
  }

  return (
    <section className="wardrobe-rail" aria-label="Add garment">
      <div className="wardrobe-rail-heading">
        <span>Add garment</span>
        <h2>Catalog clean photos</h2>
      </div>
      <div className="clean-checklist" aria-label="Clean photo checklist">
        {cleanPhotoChecklist.map((item) => <span key={item}>{item}</span>)}
      </div>
      <div className="wardrobe-upload-defaults" aria-label="Upload defaults">
        <label>
          <span>Type</span>
          <select
            value={defaults.category}
            disabled={isUploading}
            onChange={(event) => onDefaultsChange({ ...defaults, category: event.target.value as GarmentCategory })}
          >
            {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
          </select>
        </label>
        <label>
          <span>Color</span>
          <input
            value={defaults.color}
            disabled={isUploading}
            onChange={(event) => onDefaultsChange({ ...defaults, color: event.target.value })}
          />
        </label>
        <label>
          <span>Season</span>
          <input
            value={defaults.season.join(', ')}
            disabled={isUploading}
            onChange={(event) => onDefaultsChange({ ...defaults, season: splitTokens(event.target.value) })}
          />
        </label>
        <label>
          <span>Tags</span>
          <input
            value={defaults.tags.join(', ')}
            disabled={isUploading}
            onChange={(event) => onDefaultsChange({ ...defaults, tags: splitTokens(event.target.value) })}
          />
        </label>
      </div>
      <label className="wardrobe-drop-zone" aria-disabled={isUploading} onDragOver={handleDragOver} onDrop={handleDrop}>
        <CloudUpload size={24} aria-hidden="true" />
        <strong>Upload photos</strong>
        <span>Drag and drop or click to browse. JPG, PNG, WebP, up to 50 MB.</span>
        <input
          aria-label="Garment photos"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          multiple
          disabled={isUploading}
          onChange={(event) => {
            addInputFiles(event.target.files);
            event.target.value = '';
          }}
        />
      </label>
      <label className="wardrobe-camera-input">
        <Camera size={17} aria-hidden="true" />
        <span>Open camera</span>
        <input
          aria-label="Camera garment photo"
          type="file"
          accept="image/*"
          capture="environment"
          disabled={isUploading}
          onChange={(event) => {
            addInputFiles(event.target.files);
            event.target.value = '';
          }}
        />
      </label>
      <UploadQueue
        items={queue}
        disabled={isUploading}
        onAcceptTag={onAcceptTag}
        onChangeItem={onChangeItem}
        onRemove={onRemoveItem}
      />
      <button type="button" className="wardrobe-primary-button" disabled={submitDisabled} onClick={onSubmitAll}>
        <Plus size={16} aria-hidden="true" />
        {isUploading ? 'Uploading' : 'Add garments'}
      </button>
    </section>
  );
}

function splitTokens(value: string): string[] {
  return value.split(',').map((token) => token.trim()).filter(Boolean);
}
