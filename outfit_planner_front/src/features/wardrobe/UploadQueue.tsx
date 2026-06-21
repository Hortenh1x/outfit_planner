import type { GarmentCategory } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import type { UploadQueueItem, UploadQueueItemUpdates } from './wardrobeUpload';

interface UploadQueueProps {
  items: UploadQueueItem[];
  disabled?: boolean;
  onAcceptTag: (itemId: string, tag: string) => void;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onRemove: (itemId: string) => void;
}

export function UploadQueue({ items, disabled = false, onAcceptTag, onChangeItem, onRemove }: UploadQueueProps) {
  if (items.length === 0) {
    return <p className="wardrobe-rail-note">Drop several photos or use the camera input to build an upload queue.</p>;
  }

  return (
    <div className="upload-queue" aria-label="Upload queue">
      {items.map((item) => (
        <article className={item.status === 'invalid' ? 'upload-queue-row invalid' : 'upload-queue-row'} key={item.id}>
          <div className="upload-queue-heading">
            <strong>{item.file.name}</strong>
            <button type="button" aria-label={`Remove ${item.file.name}`} disabled={disabled} onClick={() => onRemove(item.id)}>
              Remove
            </button>
          </div>
          <label>
            <span>Name</span>
            <input value={item.name} disabled={disabled} onChange={(event) => onChangeItem(item.id, { name: event.target.value })} />
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
              value={item.season.join(', ')}
              disabled={disabled}
              onChange={(event) => onChangeItem(item.id, { season: splitTokens(event.target.value) })}
            />
          </label>
          <label>
            <span>Tags</span>
            <input
              value={item.tags.join(', ')}
              disabled={disabled}
              onChange={(event) => onChangeItem(item.id, { tags: splitTokens(event.target.value) })}
            />
          </label>
          <div className="suggested-tags" aria-label={`Suggested tags for ${item.name}`}>
            {item.suggestedTags.map((tag) => {
              const isAccepted = item.tags.includes(tag);

              return (
                <button
                  type="button"
                  key={tag}
                  aria-pressed={isAccepted}
                  disabled={disabled || isAccepted}
                  onClick={() => onAcceptTag(item.id, tag)}
                >
                  {tag}
                </button>
              );
            })}
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
          {item.error ? <p className="wardrobe-error" role="alert">{item.error}</p> : null}
        </article>
      ))}
    </div>
  );
}

function splitTokens(value: string): string[] {
  return value.split(',').map((token) => token.trim()).filter(Boolean);
}
