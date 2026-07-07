import { describe, expect, it } from 'vitest';
import type { GarmentAutoTagResponse } from '../../api/client';
import {
  applyAutoTagSuggestions,
  createUploadQueueItems,
  selectQueueItemsToClassify,
  updateUploadQueueItem,
  type UploadQueueItem
} from './wardrobeUpload';

function baseItem(overrides: Partial<UploadQueueItem> = {}): UploadQueueItem {
  const [item] = createUploadQueueItems(
    [new File(['x'], 'plain.png', { type: 'image/png' })],
    { category: 'Top', color: '', season: [], existingTags: [] }
  );
  return { ...item, ...overrides };
}

function suggestions(overrides: Partial<GarmentAutoTagResponse> = {}): GarmentAutoTagResponse {
  return {
    isAvailable: true,
    provider: 'test',
    category: null,
    categoryConfidence: 0,
    colors: [],
    seasons: [],
    tags: [],
    ...overrides
  };
}

function processedItem(id: string, overrides: Partial<UploadQueueItem> = {}): UploadQueueItem {
  return baseItem({
    id,
    status: 'processed',
    autoTagStatus: 'idle',
    uploadedPhoto: { fileName: 'f', contentType: 'image/png', length: 1, url: `u-${id}` },
    ...overrides
  });
}

describe('applyAutoTagSuggestions', () => {
  it('prefills untouched category, color, season, and tags', () => {
    const result = applyAutoTagSuggestions(
      baseItem(),
      suggestions({
        category: 'Dress',
        colors: [{ name: 'navy', hex: '#1f2a44', confidence: 0.6 }],
        seasons: [{ value: 'summer', confidence: 0.4 }],
        tags: [{ value: 'floral', confidence: 0.3 }]
      })
    );

    expect(result.category).toBe('Dress');
    expect(result.primaryColor).toBe('navy');
    expect(result.season).toEqual(['summer']);
    expect(result.tags).toContain('floral');
    expect(result.tags).toContain('dress');
    // Prefill must NOT mark fields as user-edited (only the user's own edits set these).
    expect(result.categoryEdited).toBe(false);
    expect(result.colorEdited).toBe(false);
    expect(result.seasonEdited).toBe(false);
  });

  it('never overwrites category, color, or season the user has edited', () => {
    const edited = baseItem({
      category: 'Bottom',
      categoryEdited: true,
      primaryColor: 'red',
      colorEdited: true,
      season: ['winter'],
      seasonEdited: true
    });

    const result = applyAutoTagSuggestions(
      edited,
      suggestions({
        category: 'Dress',
        colors: [{ name: 'navy', hex: '#1f2a44', confidence: 0.9 }],
        seasons: [{ value: 'summer', confidence: 0.9 }]
      })
    );

    expect(result.category).toBe('Bottom');
    expect(result.primaryColor).toBe('red');
    expect(result.season).toEqual(['winter']);
  });

  it('does not add model tags once the user has edited tags', () => {
    const edited = updateUploadQueueItem(baseItem(), { tags: ['mytag'], tagsEdited: true });

    const result = applyAutoTagSuggestions(edited, suggestions({ tags: [{ value: 'floral', confidence: 0.5 }] }));

    expect(result.tags).toEqual(['mytag']);
  });

  it('is a no-op when the tagger is unavailable', () => {
    const item = baseItem();
    const result = applyAutoTagSuggestions(item, suggestions({ isAvailable: false, category: 'Dress' }));
    expect(result).toBe(item);
  });
});

describe('selectQueueItemsToClassify', () => {
  it('selects processed, idle, uploaded rows up to the remaining slots', () => {
    const items = [
      processedItem('a'),
      processedItem('b', { autoTagStatus: 'classifying' }),
      processedItem('c'),
      processedItem('d', { autoTagStatus: 'done' }),
      processedItem('e', { duplicate: 'wardrobe' }),
      processedItem('f', { uploadedPhoto: null }),
      baseItem({ id: 'g', status: 'queued' })
    ];

    const started = selectQueueItemsToClassify(items, 3, new Set(['b']));

    expect(started.map((item) => item.id)).toEqual(['a', 'c']);
  });

  it('returns nothing when the concurrency limit is reached', () => {
    const items = [processedItem('a'), processedItem('b')];
    expect(selectQueueItemsToClassify(items, 1, new Set(['x']))).toHaveLength(0);
  });
});
