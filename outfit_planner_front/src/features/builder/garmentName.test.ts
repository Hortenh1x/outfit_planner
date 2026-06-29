import { describe, expect, it } from 'vitest';
import { garmentNameFromFile } from './garmentName';

describe('garmentNameFromFile', () => {
  it('turns file names into readable garment names', () => {
    expect(garmentNameFromFile(new File(['x'], 'linen-shirt.png', { type: 'image/png' }), 'Top')).toBe('linen shirt');
    expect(garmentNameFromFile(new File(['x'], '.png', { type: 'image/png' }), 'Hat')).toBe('Hat');
  });
});
