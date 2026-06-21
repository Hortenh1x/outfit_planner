import { useEffect, useState } from 'react';
import type { GarmentCategory } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import { parseTokenText, tokenListSignature, type UploadQueueItem, type UploadQueueItemUpdates } from './wardrobeUpload';

interface UploadQueueTextDraft {
  seasonText: string;
  tagsText: string;
}

interface UploadQueueProps {
  items: UploadQueueItem[];
  disabled?: boolean;
  onAcceptTag: (itemId: string, tag: string) => void;
  onChangeItem: (itemId: string, updates: UploadQueueItemUpdates) => void;
  onRemove: (itemId: string) => void;
}

export function UploadQueue({ items, disabled = false, onAcceptTag, onChangeItem, onRemove }: UploadQueueProps) {
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
                value={textDraft.seasonText}
                disabled={disabled}
                onChange={(event) => changeTextDraft(item, { seasonText: event.target.value })}
              />
            </label>
            <label>
              <span>Tags</span>
              <input
                value={textDraft.tagsText}
                disabled={disabled}
                onChange={(event) => changeTextDraft(item, { tagsText: event.target.value })}
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
        );
      })}
    </div>
  );

  function changeTextDraft(item: UploadQueueItem, updates: Partial<UploadQueueTextDraft>) {
    const nextDraft = { ...(textDrafts[item.id] ?? textDraftFromItem(item)), ...updates };
    setTextDrafts((current) => ({ ...current, [item.id]: nextDraft }));
    onChangeItem(item.id, {
      ...(updates.seasonText !== undefined ? { season: parseTokenText(nextDraft.seasonText) } : {}),
      ...(updates.tagsText !== undefined ? { tags: parseTokenText(nextDraft.tagsText) } : {})
    });
  }
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

    const tagsText = tokenListSignature(parseTokenText(existing.tagsText)) === tokenListSignature(item.tags)
      ? existing.tagsText
      : item.tags.join(', ');
    const seasonText = tokenListSignature(parseTokenText(existing.seasonText)) === tokenListSignature(item.season)
      ? existing.seasonText
      : item.season.join(', ');

    next[item.id] = { seasonText, tagsText };
    changed = changed || tagsText !== existing.tagsText || seasonText !== existing.seasonText;
  });

  changed = changed || Object.keys(current).length !== items.length;
  return changed ? next : current;
}

function textDraftFromItem(item: UploadQueueItem): UploadQueueTextDraft {
  return {
    seasonText: item.season.join(', '),
    tagsText: item.tags.join(', ')
  };
}
