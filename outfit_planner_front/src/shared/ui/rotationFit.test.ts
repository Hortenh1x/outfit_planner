import { describe, expect, it } from 'vitest';
import { fitScaleForRotation } from './rotationFit';

describe('fitScaleForRotation', () => {
  it('does not scale when the image is upright', () => {
    expect(fitScaleForRotation(0, { width: 400, height: 300 }, { width: 400, height: 300 })).toBe(1);
  });

  it('falls back to 1 until both sizes are known', () => {
    expect(fitScaleForRotation(30, null, { width: 400, height: 300 })).toBe(1);
    expect(fitScaleForRotation(30, { width: 400, height: 300 }, null)).toBe(1);
    expect(fitScaleForRotation(30, { width: 0, height: 300 }, { width: 400, height: 300 })).toBe(1);
  });

  it('shrinks a 4:3 image rotated 90 degrees to fit a 4:3 frame', () => {
    // Contained size is 400x300; at 90 degrees the bounding box is 300x400, so height drives the fit.
    expect(fitScaleForRotation(90, { width: 400, height: 300 }, { width: 400, height: 300 })).toBeCloseTo(0.75, 5);
  });

  it('is symmetric in the sign of the angle', () => {
    const frame = { width: 400, height: 300 };
    const natural = { width: 800, height: 600 };
    expect(fitScaleForRotation(-37, natural, frame)).toBeCloseTo(fitScaleForRotation(37, natural, frame), 10);
  });

  it('keeps the rotated bounding box within the frame for a range of angles', () => {
    const frame = { width: 400, height: 300 };
    const natural = { width: 900, height: 1200 };
    const contain = Math.min(frame.width / natural.width, frame.height / natural.height, 1);
    const displayedWidth = natural.width * contain;
    const displayedHeight = natural.height * contain;

    for (const angle of [1, 8, 15, 45, 60, 90, 137, 180]) {
      const scale = fitScaleForRotation(angle, natural, frame);
      const radians = (Math.abs(angle) * Math.PI) / 180;
      const cos = Math.abs(Math.cos(radians));
      const sin = Math.abs(Math.sin(radians));
      const boxWidth = scale * (displayedWidth * cos + displayedHeight * sin);
      const boxHeight = scale * (displayedWidth * sin + displayedHeight * cos);
      // Allow a sub-pixel tolerance for floating point.
      expect(boxWidth).toBeLessThanOrEqual(frame.width + 1e-6);
      expect(boxHeight).toBeLessThanOrEqual(frame.height + 1e-6);
    }
  });
});
