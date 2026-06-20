import type { GarmentCategory } from '../../types';
import { validateUploadImageFile } from '../uploads/imageFile';

export type UploadQueueStatus = 'ready' | 'invalid' | 'uploading' | 'uploaded' | 'failed';

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
  category: GarmentCategory;
  tags: string[];
  suggestedTags: string[];
  primaryColor: string;
  season: string[];
  warnings: string[];
  validationError: string | null;
  status: UploadQueueStatus;
  error: string | null;
  previewUrl?: string;
}

export interface SuggestedTagInput {
  fileName: string;
  category: GarmentCategory;
  color: string;
  season: string[];
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
  const suggestedTags = suggestTagsForUpload({
    fileName: file.name,
    category: defaults.category,
    color: defaults.color,
    season: defaults.season,
    existingTags: defaults.existingTags
  });

  return {
    id: `${Date.now()}-${index}-${file.name}`,
    file,
    name: inferGarmentName(file.name),
    category: defaults.category,
    tags: suggestedTags,
    suggestedTags,
    primaryColor: defaults.color,
    season: [...defaults.season],
    warnings: getPhotoQualityWarnings(file),
    validationError,
    status: validationError ? 'invalid' : 'ready',
    error: null
  };
}

export function updateUploadQueueItem(item: UploadQueueItem, updates: Partial<Omit<UploadQueueItem, 'id' | 'file'>>): UploadQueueItem {
  return { ...item, ...updates };
}

export function validateQueueFile(file: File): string | null {
  try {
    validateUploadImageFile(file);
    return null;
  } catch (error) {
    return (error as Error).message;
  }
}

export function suggestTagsForUpload(input: SuggestedTagInput): string[] {
  const fromFileName = tokenizeFileName(input.fileName);
  const category = input.category.toLowerCase();
  const tokens = [
    ...fromFileName,
    input.color,
    category,
    ...input.season,
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
  return withoutExtension
    .split(/[^a-zA-Z0-9]+/)
    .map((token) => token.trim().toLowerCase())
    .filter((token) => token.length > 1 && !isGenericToken(token));
}

function uniqueTokens(tokens: string[]): string[] {
  const seen = new Set<string>();
  return tokens
    .map((token) => token.trim().toLowerCase())
    .filter((token) => token.length > 0)
    .filter((token) => {
      if (seen.has(token)) {
        return false;
      }

      seen.add(token);
      return true;
    });
}

function isGenericFileName(fileName: string): boolean {
  const name = fileName.replace(/\.[^.]+$/, '').toLowerCase();
  return /^(img|image|photo|dsc|pxl)[_-]?\d*$/i.test(name);
}

function isGenericToken(token: string): boolean {
  return ['img', 'image', 'photo', 'dsc', 'pxl', 'jpeg', 'jpg', 'png', 'webp'].includes(token);
}
