import type { DragEvent } from 'react';
import { Camera, CloudUpload, Plus } from 'lucide-react';
import { UploadQueue } from './UploadQueue';
import { cleanPhotoChecklist, type UploadQueueItem } from './wardrobeUpload';

type UploadQueueItemUpdates = Partial<Pick<UploadQueueItem, 'name' | 'category' | 'tags' | 'primaryColor' | 'season'>>;

interface WardrobeUploadPanelProps {
  queue: UploadQueueItem[];
  isUploading: boolean;
  onAcceptTag: (itemId: string, tag: string) => void;
  onAddFiles: (files: File[]) => void;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onRemoveItem: (itemId: string) => void;
  onSubmitAll: () => void;
}

export function WardrobeUploadPanel({
  queue,
  isUploading,
  onAcceptTag,
  onAddFiles,
  onChangeItem,
  onRemoveItem,
  onSubmitAll
}: WardrobeUploadPanelProps) {
  const submitDisabled = isUploading || queue.every((item) => item.status === 'invalid');

  function addInputFiles(fileList: FileList | null) {
    onAddFiles(Array.from(fileList ?? []));
  }

  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
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
      <label className="wardrobe-drop-zone" onDragOver={(event) => event.preventDefault()} onDrop={handleDrop}>
        <CloudUpload size={24} aria-hidden="true" />
        <strong>Upload photos</strong>
        <span>Drag and drop or click to browse. JPG, PNG, WebP, up to 50 MB.</span>
        <input
          aria-label="Garment photos"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          multiple
          onChange={(event) => addInputFiles(event.target.files)}
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
          onChange={(event) => addInputFiles(event.target.files)}
        />
      </label>
      <UploadQueue items={queue} onAcceptTag={onAcceptTag} onChangeItem={onChangeItem} onRemove={onRemoveItem} />
      <button type="button" className="wardrobe-primary-button" disabled={submitDisabled} onClick={onSubmitAll}>
        <Plus size={16} aria-hidden="true" />
        {isUploading ? 'Uploading' : 'Add garments'}
      </button>
    </section>
  );
}
