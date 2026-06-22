import type { TryOnMode } from '../../types';

export function creditsLabel(credits: number): string {
  if (credits === 0) {
    return 'Free';
  }

  return credits === 1 ? '1 credit' : `${credits} credits`;
}

export function modeLabel(mode: TryOnMode): string {
  switch (mode) {
    case 'ClothesOnlyPreview':
      return 'Clothes only';
    case 'SingleGarmentTryOn':
      return 'Single garment';
    case 'SequentialOutfitTryOn':
      return 'Sequential outfit';
    case 'ExperimentalCompositeTryOn':
      return 'Composite premium';
  }
}
