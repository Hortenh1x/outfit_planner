import type { UploadedPhotoResponse } from '../../api/client';
import type { GarmentCategory } from '../../types';
import { validateUploadImageFile } from '../uploads/imageFile';

export type UploadQueueStatus = 'invalid' | 'queued' | 'processing' | 'processed' | 'failed';

export interface UploadQueueDefaults {
  category: GarmentCategory;
  color: string;
  season: string[];
  existingTags: string[];
}

export interface UploadQueueItem {
  id: string;
  file: File;
  name: string;
  nameEdited: boolean;
  category: GarmentCategory;
  tags: string[];
  tagsEdited: boolean;
  suggestedTags: string[];
  existingTags: string[];
  primaryColor: string;
  season: string[];
  warnings: string[];
  validationError: string | null;
  status: UploadQueueStatus;
  error: string | null;
  uploadedPhoto?: UploadedPhotoResponse | null;
  previewUrl?: string;
}

export type UploadQueueItemUpdates = Partial<Pick<UploadQueueItem, 'name' | 'nameEdited' | 'category' | 'tags' | 'tagsEdited' | 'primaryColor' | 'season'>>;

export interface SuggestedTagInput {
  fileName: string;
  name?: string;
  category: GarmentCategory;
  color: string;
  season: string[];
  selectedTags?: string[];
  existingTags: string[];
}

export interface ImageDimensions {
  width: number;
  height: number;
}

export const cleanPhotoChecklist = [
  'Front view',
  'Good lighting',
  'No background clutter'
];

export function createUploadQueueItems(files: File[], defaults: UploadQueueDefaults): UploadQueueItem[] {
  return files.map((file, index) => createUploadQueueItem(file, defaults, index));
}

export function createUploadQueueItem(file: File, defaults: UploadQueueDefaults, index = 0): UploadQueueItem {
  const validationError = validateQueueFile(file);
  const name = inferGarmentName(file.name);
  const suggestedTags = suggestTagsForUpload({
    fileName: file.name,
    name,
    category: defaults.category,
    color: defaults.color,
    season: defaults.season,
    existingTags: defaults.existingTags
  });

  return {
    id: createUploadQueueItemId(file.name, index),
    file,
    name,
    nameEdited: false,
    category: defaults.category,
    tags: suggestedTags,
    tagsEdited: false,
    suggestedTags,
    existingTags: [...defaults.existingTags],
    primaryColor: defaults.color,
    season: [...defaults.season],
    warnings: getPhotoQualityWarnings(file),
    validationError,
    status: validationError ? 'invalid' : 'queued',
    error: null,
    uploadedPhoto: null
  };
}

export function updateUploadQueueItem(item: UploadQueueItem, updates: Partial<Omit<UploadQueueItem, 'id' | 'file'>>): UploadQueueItem {
  const tagsEdited = updates.tagsEdited ?? item.tagsEdited;
  const nameEdited = updates.nameEdited ?? item.nameEdited;
  const next = { ...item, ...updates, tagsEdited, nameEdited };
  const suggestedTags = tagsEdited
    ? uniqueTokens(next.tags)
    : suggestTagsForUpload({
        fileName: nameEdited ? '' : item.file.name,
        name: next.name,
        category: next.category,
        color: next.primaryColor,
        season: next.season,
        existingTags: next.existingTags
      });

  return { ...next, tags: suggestedTags, suggestedTags };
}

export function validateQueueFile(file: File): string | null {
  try {
    validateUploadImageFile(file);
    return null;
  } catch (error) {
    return (error as Error).message;
  }
}

/**
 * Returns the queued items that should start background processing now, given a
 * concurrency `limit` and the ids of uploads already in flight. The caller owns
 * the in-flight set so this stays a pure, deterministic selection.
 */
export function selectQueueItemsToStart(
  items: UploadQueueItem[],
  limit: number,
  inFlightIds: ReadonlySet<string>
): UploadQueueItem[] {
  const availableSlots = limit - inFlightIds.size;
  if (availableSlots <= 0) {
    return [];
  }

  return items
    .filter((item) => item.status === 'queued' && !inFlightIds.has(item.id))
    .slice(0, availableSlots);
}

export function isQueueProcessing(items: UploadQueueItem[]): boolean {
  return items.some((item) => item.status === 'queued' || item.status === 'processing');
}

export function hasCreatableItems(items: UploadQueueItem[]): boolean {
  return items.some((item) => item.status === 'processed' || item.status === 'failed');
}

export function suggestTagsForUpload(input: SuggestedTagInput): string[] {
  const fromFileName = tokenizeFileName(input.fileName);
  const fromName = input.name ? tokenizeText(input.name) : [];
  const category = input.category.toLowerCase();
  const tokens = [
    ...fromFileName,
    ...fromName,
    input.color,
    category,
    ...input.season,
    ...(input.selectedTags ?? []),
    ...input.existingTags
  ];

  return uniqueTokens(tokens);
}

export function inferGarmentName(fileName: string): string {
  const tokens = tokenizeFileName(fileName);
  if (tokens.length === 0) {
    return 'New garment';
  }

  const name = tokens.join(' ');
  return name.charAt(0).toUpperCase() + name.slice(1);
}

export function getPhotoQualityWarnings(file: File, dimensions?: ImageDimensions): string[] {
  const warnings: string[] = [];

  if (file.size < 1024) {
    warnings.push('The file is tiny; confirm the photo is not a placeholder or compressed preview.');
  }

  if (dimensions && (dimensions.width < 600 || dimensions.height < 600)) {
    warnings.push('Image dimensions are small; use a sharper front-view photo if possible.');
  }

  if (dimensions) {
    const ratio = dimensions.width / dimensions.height;
    if (ratio > 2.2 || ratio < 0.45) {
      warnings.push('The photo is very tall or wide; crop around the garment before uploading.');
    }
  }

  if (isGenericFileName(file.name)) {
    warnings.push('The filename is generic; confirm the generated name and tags before saving.');
  }

  return warnings;
}

function tokenizeFileName(fileName: string): string[] {
  const withoutExtension = fileName.replace(/\.[^.]+$/, '');
  return tokenizeText(withoutExtension);
}

function tokenizeText(value: string): string[] {
  return value
    .split(/[^a-zA-Z0-9]+/)
    .map((token) => token.trim().toLowerCase())
    .filter((token) => token.length > 1 && !isGenericToken(token));
}

export function normalizeTagToken(value: string): string {
  return value.trim().toLowerCase();
}

export function parseTokenText(value: string): string[] {
  return value
    .split(',')
    .map((token) => token.trim())
    .filter(Boolean);
}

export function uniqueTokens(tokens: string[]): string[] {
  const seen = new Set<string>();
  return tokens
    .map(normalizeTagToken)
    .filter((token) => token.length > 0)
    .filter((token) => {
      if (seen.has(token)) {
        return false;
      }

      seen.add(token);
      return true;
    });
}

function createUploadQueueItemId(fileName: string, index: number): string {
  return `${Date.now()}-${index}-${createRandomId()}-${fileName}`;
}

function createRandomId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  return Math.random().toString(36).slice(2, 10);
}

function isGenericFileName(fileName: string): boolean {
  const name = fileName.replace(/\.[^.]+$/, '').toLowerCase();
  return /^(img|image|photo|dsc|pxl)[_-]?\d*$/i.test(name);
}

function isGenericToken(token: string): boolean {
  return ['img', 'image', 'photo', 'dsc', 'pxl', 'jpeg', 'jpg', 'png', 'webp'].includes(token);
}
