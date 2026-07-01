import { type DragEvent } from 'react';
import { Camera, CloudUpload, Plus } from 'lucide-react';
import type { GarmentCategory } from '../../types';
import { UploadQueue } from './UploadQueue';
import {
  hasCreatableItems,
  isQueueProcessing,
  type UploadQueueItem,
  type UploadQueueItemUpdates
} from './wardrobeUpload';

export interface WardrobeUploadDefaults {
  category: GarmentCategory;
  color: string;
  season: string[];
  tags: string[];
}

interface WardrobeUploadPanelProps {
  queue: UploadQueueItem[];
  isUploading: boolean;
  onAddFiles: (files: File[]) => void;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onRemoveItem: (itemId: string) => void;
  onRetryItem: (itemId: string) => void;
  onSubmitAll: () => void;
}

export function WardrobeUploadPanel({
  queue,
  isUploading,
  onAddFiles,
  onChangeItem,
  onRemoveItem,
  onRetryItem,
  onSubmitAll
}: WardrobeUploadPanelProps) {
  const processing = isQueueProcessing(queue);
  // Non-blocking: you can add the ready photos even while others are still uploading; background
  // removal then runs asynchronously on the server and the cutout appears in the wardrobe later.
  const submitDisabled = isUploading || !hasCreatableItems(queue);
  const submitLabel = isUploading ? 'Uploading' : processing ? 'Add ready garments' : 'Add garments';

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
        <h2>Add garment</h2>
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
        onChangeItem={onChangeItem}
        onRemove={onRemoveItem}
        onRetry={onRetryItem}
      />
      <button type="button" className="wardrobe-primary-button" disabled={submitDisabled} onClick={onSubmitAll}>
        <Plus size={16} aria-hidden="true" />
        {submitLabel}
      </button>
    </section>
  );
}
