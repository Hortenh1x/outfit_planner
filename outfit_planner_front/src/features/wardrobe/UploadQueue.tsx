import { useEffect, useState } from 'react';
import { Loader2, RotateCcw } from 'lucide-react';
import type { GarmentCategory } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import { isSupportedImageFile } from '../uploads/imageFile';
import { TagChipsEditor } from './TagChipsEditor';
import { parseTokenText, type UploadQueueItem, type UploadQueueItemUpdates } from './wardrobeUpload';

interface UploadQueueTextDraft {
  seasonText: string;
}

interface UploadQueueProps {
  items: UploadQueueItem[];
  disabled?: boolean;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onRemove: (itemId: string) => void;
  onRetry: (itemId: string) => void;
}

export function UploadQueue({ items, disabled = false, onChangeItem, onRemove, onRetry }: UploadQueueProps) {
  const [textDrafts, setTextDrafts] = useState<Record<string, UploadQueueTextDraft>>(() => createTextDrafts(items));

  useEffect(() => {
    setTextDrafts((current) => syncTextDrafts(current, items));
  }, [items]);

  if (items.length === 0) {
    return <p className="wardrobe-rail-note">Drop several photos or use the camera input to build an upload queue.</p>;
  }

  return (
    <div className="upload-queue" aria-label="Upload queue">
      {items.map((item) => {
        const textDraft = textDrafts[item.id] ?? textDraftFromItem(item);

        return (
          <article className={item.status === 'invalid' ? 'upload-queue-row invalid' : 'upload-queue-row'} key={item.id}>
            <UploadQueuePreview item={item} />
            <div className="upload-queue-heading">
              <strong>{item.file.name}</strong>
              <button type="button" aria-label={`Remove ${item.file.name}`} disabled={disabled} onClick={() => onRemove(item.id)}>
                Remove
              </button>
            </div>
            <label>
              <span>Name</span>
              <input
                value={item.name}
                disabled={disabled}
                onChange={(event) => onChangeItem(item.id, { name: event.target.value, nameEdited: true })}
              />
            </label>
            <label>
              <span>Type</span>
              <select
                value={item.category}
                disabled={disabled}
                onChange={(event) => onChangeItem(item.id, { category: event.target.value as GarmentCategory })}
              >
                {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
              </select>
            </label>
            <label>
              <span>Color</span>
              <input
                value={item.primaryColor}
                disabled={disabled}
                onChange={(event) => onChangeItem(item.id, { primaryColor: event.target.value })}
              />
            </label>
            <label>
              <span>Season</span>
              <input
                value={textDraft.seasonText}
                disabled={disabled}
                onChange={(event) => changeTextDraft(item, { seasonText: event.target.value })}
              />
            </label>
            <div className="upload-queue-field">
              <span className="upload-queue-field-label">Tags</span>
              <TagChipsEditor
                tags={item.tags}
                existingTags={item.existingTags}
                disabled={disabled}
                ariaLabel={`Tags for ${item.name}`}
                onChange={(tags) => onChangeItem(item.id, { tags, tagsEdited: true })}
              />
            </div>
            {item.validationError ? <p className="wardrobe-error" role="alert">{item.validationError}</p> : null}
            {item.warnings.length > 0 ? (
              <div className="wardrobe-warning" role="status" aria-label={`Photo warnings for ${item.name}`}>
                <strong>Needs better photo?</strong>
                <ul>
                  {item.warnings.map((warning) => <li key={warning}>{warning}</li>)}
                </ul>
              </div>
            ) : null}
            {item.error ? (
              <div className="wardrobe-error upload-queue-error" role="alert">
                <span>{item.error}</span>
                <button type="button" className="upload-queue-retry" disabled={disabled} onClick={() => onRetry(item.id)}>
                  <RotateCcw size={13} aria-hidden="true" />
                  Retry
                </button>
              </div>
            ) : null}
          </article>
        );
      })}
    </div>
  );

  function changeTextDraft(item: UploadQueueItem, updates: Partial<UploadQueueTextDraft>) {
    const nextDraft = { ...(textDrafts[item.id] ?? textDraftFromItem(item)), ...updates };
    setTextDrafts((current) => ({ ...current, [item.id]: nextDraft }));
    if (updates.seasonText !== undefined) {
      onChangeItem(item.id, { season: parseTokenText(nextDraft.seasonText) });
    }
  }
}

function UploadQueuePreview({ item }: { item: UploadQueueItem }) {
  const [localPreviewUrl, setLocalPreviewUrl] = useState(item.previewUrl ?? '');
  const [cutoutFailed, setCutoutFailed] = useState(false);

  useEffect(() => {
    if (item.previewUrl) {
      setLocalPreviewUrl(item.previewUrl);
      return undefined;
    }

    if (!isSupportedImageFile(item.file) || typeof URL.createObjectURL !== 'function') {
      setLocalPreviewUrl('');
      return undefined;
    }

    const objectUrl = URL.createObjectURL(item.file);
    setLocalPreviewUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [item.file, item.previewUrl]);

  const isProcessing = item.status === 'queued' || item.status === 'processing';
  const cutoutUrl = !cutoutFailed && item.status === 'processed'
    ? item.uploadedPhoto?.cutoutUrl || item.uploadedPhoto?.thumbnailUrl || item.uploadedPhoto?.url || ''
    : '';
  const displayUrl = cutoutUrl || localPreviewUrl;

  if (!displayUrl) {
    return isProcessing ? (
      <div className="upload-queue-preview is-empty">
        <ProcessingOverlay />
      </div>
    ) : null;
  }

  return (
    <div className={cutoutUrl ? 'upload-queue-preview is-cutout' : 'upload-queue-preview'}>
      <img
        src={displayUrl}
        alt={`Preview of ${item.file.name}`}
        onError={() => {
          if (cutoutUrl) {
            setCutoutFailed(true);
          }
        }}
      />
      {isProcessing ? <ProcessingOverlay /> : null}
    </div>
  );
}

function ProcessingOverlay() {
  return (
    <div className="upload-queue-preview-overlay" role="status">
      <Loader2 size={18} aria-hidden="true" className="upload-queue-spinner" />
      <span>Removing background…</span>
    </div>
  );
}

function createTextDrafts(items: UploadQueueItem[]): Record<string, UploadQueueTextDraft> {
  return items.reduce((drafts, item) => {
    drafts[item.id] = textDraftFromItem(item);
    return drafts;
  }, {} as Record<string, UploadQueueTextDraft>);
}

function syncTextDrafts(
  current: Record<string, UploadQueueTextDraft>,
  items: UploadQueueItem[]
): Record<string, UploadQueueTextDraft> {
  let changed = false;
  const next: Record<string, UploadQueueTextDraft> = {};

  items.forEach((item) => {
    const existing = current[item.id];
    if (!existing) {
      next[item.id] = textDraftFromItem(item);
      changed = true;
      return;
    }

    const seasonText = tokenListSignature(parseTokenText(existing.seasonText)) === tokenListSignature(item.season)
      ? existing.seasonText
      : item.season.join(', ');

    next[item.id] = { seasonText };
    changed = changed || seasonText !== existing.seasonText;
  });

  changed = changed || Object.keys(current).length !== items.length;
  return changed ? next : current;
}

function textDraftFromItem(item: UploadQueueItem): UploadQueueTextDraft {
  return {
    seasonText: item.season.join(', ')
  };
}

function tokenListSignature(tokens: string[]): string {
  return JSON.stringify(tokens);
}
