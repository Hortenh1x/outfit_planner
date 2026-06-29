import { describe, expect, it } from 'vitest';
import {
  cleanPhotoChecklist,
  createUploadQueueItems,
  getPhotoQualityWarnings,
  hasCreatableItems,
  isQueueProcessing,
  normalizeTagToken,
  parseTokenText,
  selectQueueItemsToStart,
  suggestTagsForUpload,
  uniqueTokens,
  updateUploadQueueItem,
  type UploadQueueItem,
  type UploadQueueStatus
} from './wardrobeUpload';

function queueItem(id: string, status: UploadQueueStatus): UploadQueueItem {
  const [item] = createUploadQueueItems(
    [new File(['x'], `${id}.png`, { type: 'image/png' })],
    { category: 'Top', color: '', season: [], existingTags: [] }
  );
  return { ...item, id, status };
}

describe('wardrobeUpload', () => {
  it('exposes the clean photo checklist copy required before upload', () => {
    expect(cleanPhotoChecklist).toEqual([
      'Front view',
      'Good lighting',
      'No background clutter'
    ]);
  });

  it('creates editable queue rows from multiple supported image files', () => {
    const files = [
      new File(['shirt'], 'black-silk-cami.png', { type: 'image/png' }),
      new File(['coat'], 'wool-blazer.webp', { type: 'image/webp' })
    ];

    const rows = createUploadQueueItems(files, {
      category: 'Top',
      color: 'black',
      season: ['summer'],
      existingTags: ['favorite']
    });

    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({
      file: files[0],
      name: 'Black silk cami',
      category: 'Top',
      tags: ['black', 'silk', 'cami', 'top', 'summer', 'favorite'],
      primaryColor: 'black',
      season: ['summer'],
      status: 'queued',
      validationError: null
    });
    expect(rows[1].name).toBe('Wool blazer');
  });

  it('keeps unsupported files in the queue with a validation error', () => {
    const rows = createUploadQueueItems([
      new File(['notes'], 'notes.txt', { type: 'text/plain' })
    ], { category: 'Accessory', color: '', season: [], existingTags: [] });

    expect(rows[0]).toMatchObject({
      name: 'Notes',
      category: 'Accessory',
      status: 'invalid',
      validationError: 'Upload a JPG, PNG, or WebP image.'
    });
  });

  it('suggests tags from filename category color season and existing tags', () => {
    expect(suggestTagsForUpload({
      fileName: 'cream-linen-shirt.JPG',
      name: 'Soft office layer',
      category: 'Top',
      color: 'cream',
      season: ['spring', 'summer'],
      existingTags: ['work']
    })).toEqual(['cream', 'linen', 'shirt', 'soft', 'office', 'layer', 'top', 'spring', 'summer', 'work']);
  });

  it('adds advisory photo warnings for weak upload candidates', () => {
    const warnings = getPhotoQualityWarnings(
      new File(['x'], 'IMG_0001.png', { type: 'image/png' }),
      { width: 320, height: 1200 }
    );

    expect(warnings).toContain('Image dimensions are small; use a sharper front-view photo if possible.');
    expect(warnings).toContain('The photo is very tall or wide; crop around the garment before uploading.');
    expect(warnings).toContain('The filename is generic; confirm the generated name and tags before saving.');
    expect(warnings).toContain('The file is tiny; confirm the photo is not a placeholder or compressed preview.');
  });

  it('updates a queue row without mutating the existing row', () => {
    const [row] = createUploadQueueItems([
      new File(['shirt'], 'black-shirt.png', { type: 'image/png' })
    ], { category: 'Top', color: 'black', season: [], existingTags: [] });

    const updated = updateUploadQueueItem(row, { name: 'Black evening shirt', tags: ['black', 'evening'], tagsEdited: true });

    expect(updated).toMatchObject({ name: 'Black evening shirt', tags: ['black', 'evening'] });
    expect(row.name).toBe('Black shirt');
  });

  it('recomputes suggested tags from edited queue row fields in real time', () => {
    const [row] = createUploadQueueItems([
      new File(['shirt'], 'black-shirt.png', { type: 'image/png' })
    ], { category: 'Top', color: 'black', season: ['summer'], existingTags: ['work'] });

    const updated = updateUploadQueueItem(row, {
      name: 'Ruby office jacket',
      nameEdited: true,
      category: 'Outerwear',
      primaryColor: 'ruby',
      season: ['fall']
    });

    expect(updated.suggestedTags).toEqual([
      'ruby',
      'office',
      'jacket',
      'outerwear',
      'fall',
      'work'
    ]);
    expect(updated.tags).toEqual(updated.suggestedTags);
    expect(row.suggestedTags).toEqual(['black', 'shirt', 'top', 'summer', 'work']);
  });

  it('drops filename-derived tags after the upload row name is cleared manually', () => {
    const [row] = createUploadQueueItems([
      new File(['shirt'], '2026-06-23-221835552.png', { type: 'image/png' })
    ], { category: 'Top', color: '', season: [], existingTags: [] });

    const updated = updateUploadQueueItem(row, { name: '', nameEdited: true });

    expect(updated.tags).toEqual(['top']);
    expect(updated.suggestedTags).toEqual(['top']);
  });

  it('uses manually edited tags as the live visible tag chips', () => {
    const [row] = createUploadQueueItems([
      new File(['shirt'], 'plain-shirt.png', { type: 'image/png' })
    ], { category: 'Top', color: 'black', season: ['summer'], existingTags: ['work'] });

    const updated = updateUploadQueueItem(row, { tags: ['formal'], tagsEdited: true });

    expect(updated.suggestedTags).toEqual(['formal']);
    expect(updated.tags).toEqual(['formal']);
  });

  it('uses unique row ids across separate queue creation calls', () => {
    const originalDateNow = Date.now;
    Date.now = () => 123;

    try {
      const defaults = { category: 'Top' as const, color: 'black', season: [], existingTags: [] };
      const [first] = createUploadQueueItems([
        new File(['shirt'], 'black-shirt.png', { type: 'image/png' })
      ], defaults);
      const [second] = createUploadQueueItems([
        new File(['shirt'], 'black-shirt.png', { type: 'image/png' })
      ], defaults);

      expect(first.id).not.toBe(second.id);
    } finally {
      Date.now = originalDateNow;
    }
  });

  it('selects queued items up to the remaining concurrency slots', () => {
    const items = [
      queueItem('a', 'queued'),
      queueItem('b', 'processing'),
      queueItem('c', 'queued'),
      queueItem('d', 'queued'),
      queueItem('e', 'processed')
    ];

    const started = selectQueueItemsToStart(items, 3, new Set(['b']));

    expect(started.map((item) => item.id)).toEqual(['a', 'c']);
  });

  it('skips items already in flight and returns nothing when the limit is reached', () => {
    const items = [queueItem('a', 'queued'), queueItem('b', 'queued')];

    expect(selectQueueItemsToStart(items, 2, new Set(['a'])).map((item) => item.id)).toEqual(['b']);
    expect(selectQueueItemsToStart(items, 1, new Set(['x']))).toHaveLength(0);
  });

  it('reports whether the queue is still processing photos', () => {
    expect(isQueueProcessing([queueItem('a', 'processed'), queueItem('b', 'failed')])).toBe(false);
    expect(isQueueProcessing([queueItem('a', 'processed'), queueItem('b', 'queued')])).toBe(true);
    expect(isQueueProcessing([queueItem('a', 'processing')])).toBe(true);
  });

  it('reports whether any item can be turned into a garment', () => {
    expect(hasCreatableItems([queueItem('a', 'invalid'), queueItem('b', 'processing')])).toBe(false);
    expect(hasCreatableItems([queueItem('a', 'processed')])).toBe(true);
    expect(hasCreatableItems([queueItem('a', 'failed')])).toBe(true);
  });

  it('normalizes and parses tag tokens consistently', () => {
    expect(normalizeTagToken('  Linen ')).toBe('linen');
    expect(parseTokenText('a, B , ,c')).toEqual(['a', 'B', 'c']);
    expect(uniqueTokens(['A', 'a', ' b ', 'b'])).toEqual(['a', 'b']);
  });
});
