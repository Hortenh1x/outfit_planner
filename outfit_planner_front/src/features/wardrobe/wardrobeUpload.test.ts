import { describe, expect, it } from 'vitest';
import {
  cleanPhotoChecklist,
  createUploadQueueItems,
  getPhotoQualityWarnings,
  suggestTagsForUpload,
  updateUploadQueueItem
} from './wardrobeUpload';

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
      status: 'ready',
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
      category: 'Top',
      color: 'cream',
      season: ['spring', 'summer'],
      existingTags: ['work']
    })).toEqual(['cream', 'linen', 'shirt', 'top', 'spring', 'summer', 'work']);
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

    const updated = updateUploadQueueItem(row, { name: 'Black evening shirt', tags: ['black', 'evening'] });

    expect(updated).toMatchObject({ name: 'Black evening shirt', tags: ['black', 'evening'] });
    expect(row.name).toBe('Black shirt');
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
});
