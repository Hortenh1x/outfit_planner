import { describe, expect, it } from 'vitest';
import { creditsLabel, modeLabel } from './tryOnText';

describe('try-on text helpers', () => {
  it('formats credit counts and mode labels', () => {
    expect(creditsLabel(0)).toBe('Free');
    expect(creditsLabel(1)).toBe('1 credit');
    expect(creditsLabel(3)).toBe('3 credits');
    expect(modeLabel('ClothesOnlyPreview')).toBe('Clothes only');
    expect(modeLabel('SingleGarmentTryOn')).toBe('Single garment');
    expect(modeLabel('SequentialOutfitTryOn')).toBe('Sequential outfit');
    expect(modeLabel('ExperimentalCompositeTryOn')).toBe('Composite premium');
  });
});
