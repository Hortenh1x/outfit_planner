export interface Size {
  width: number;
  height: number;
}

/**
 * The largest uniform scale that keeps an image of `natural` size, displayed with `object-fit: contain`
 * inside `frame` and then rotated by `angleDeg`, fully within that frame. Rotating a rectangle grows its
 * bounding box, so without this the corners would spill out (and get clipped by the frame); scaling down
 * by this factor keeps the whole garment visible instead. Never upscales past the contained size, and
 * falls back to 1 until both sizes are known (e.g. before the image has loaded or the frame is measured).
 */
export function fitScaleForRotation(angleDeg: number, natural: Size | null, frame: Size | null): number {
  if (!natural || !frame) {
    return 1;
  }
  if (natural.width <= 0 || natural.height <= 0 || frame.width <= 0 || frame.height <= 0) {
    return 1;
  }

  // Size the image the way `object-fit: contain` + `max-*: 100%` renders it: fit inside the frame,
  // never enlarged beyond its natural pixels.
  const contain = Math.min(frame.width / natural.width, frame.height / natural.height, 1);
  const displayedWidth = natural.width * contain;
  const displayedHeight = natural.height * contain;

  const radians = (Math.abs(angleDeg) * Math.PI) / 180;
  const cos = Math.abs(Math.cos(radians));
  const sin = Math.abs(Math.sin(radians));
  const rotatedWidth = displayedWidth * cos + displayedHeight * sin;
  const rotatedHeight = displayedWidth * sin + displayedHeight * cos;

  if (rotatedWidth <= 0 || rotatedHeight <= 0) {
    return 1;
  }
  return Math.min(frame.width / rotatedWidth, frame.height / rotatedHeight, 1);
}
