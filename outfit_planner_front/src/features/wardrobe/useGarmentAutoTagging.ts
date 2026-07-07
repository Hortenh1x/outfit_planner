import { useCallback, useMemo, useRef } from 'react';
import { classifyGarmentPhoto } from '../../api/client';
import { applyAutoTagSuggestions, selectQueueItemsToClassify, type UploadQueueItem } from './wardrobeUpload';

/**
 * Auto-tags a garment row as soon as its photo has been processed, prefilling category / color /
 * season / tags before the user submits. Mirrors the eager background-removal orchestration:
 * client-driven, concurrency-limited, and aborted when a row is removed. Suggestions only fill
 * fields the user has not touched (see `applyAutoTagSuggestions`), so user edits always win.
 */
const MAX_CONCURRENT_CLASSIFICATIONS = 3;

type QueueUpdater = (updater: (items: UploadQueueItem[]) => UploadQueueItem[]) => void;

export interface GarmentAutoTagging {
  /** Classify any `processed`, not-yet-tagged rows that fit within the concurrency limit. */
  start: (items: UploadQueueItem[], knownTags: string[]) => void;
  /** Abort an in-flight classification (used when a row is removed from the queue). */
  abort: (itemId: string) => void;
}

export function useGarmentAutoTagging(setQueue: QueueUpdater): GarmentAutoTagging {
  // The controller map doubles as the in-flight guard, so StrictMode's double-invoked effects
  // cannot start the same classification twice.
  const controllersRef = useRef<Map<string, AbortController>>(new Map());

  const start = useCallback(
    (items: UploadQueueItem[], knownTags: string[]) => {
      const inFlightIds = new Set(controllersRef.current.keys());
      const toStart = selectQueueItemsToClassify(items, MAX_CONCURRENT_CLASSIFICATIONS, inFlightIds);
      if (toStart.length === 0) {
        return;
      }

      const startingIds = new Set(toStart.map((item) => item.id));
      setQueue((current) =>
        current.map((item) => (startingIds.has(item.id) ? { ...item, autoTagStatus: 'classifying' } : item))
      );

      for (const item of toStart) {
        const imageUrl = classificationImageUrl(item);
        if (!imageUrl) {
          // Nothing to classify against; mark done so the queue does not keep retrying.
          setQueue((current) =>
            current.map((queued) => (queued.id === item.id ? { ...queued, autoTagStatus: 'done' } : queued))
          );
          continue;
        }

        const controller = new AbortController();
        controllersRef.current.set(item.id, controller);

        classifyGarmentPhoto(imageUrl, knownTags, controller.signal)
          .then((suggestions) => {
            controllersRef.current.delete(item.id);
            setQueue((current) =>
              current.map((queued) =>
                queued.id === item.id
                  ? { ...applyAutoTagSuggestions(queued, suggestions), autoTagStatus: 'done' }
                  : queued
              )
            );
          })
          .catch(() => {
            controllersRef.current.delete(item.id);
            if (controller.signal.aborted) {
              return;
            }

            // Prefill is best-effort; a failure just leaves the manually-entered defaults in place.
            setQueue((current) =>
              current.map((queued) => (queued.id === item.id ? { ...queued, autoTagStatus: 'failed' } : queued))
            );
          });
      }
    },
    [setQueue]
  );

  const abort = useCallback((itemId: string) => {
    const controller = controllersRef.current.get(itemId);
    if (controller) {
      controller.abort();
      controllersRef.current.delete(itemId);
    }
  }, []);

  return useMemo(() => ({ start, abort }), [start, abort]);
}

function classificationImageUrl(item: UploadQueueItem): string | null {
  const photo = item.uploadedPhoto;
  if (!photo) {
    return null;
  }

  // The backend resolves the fileName from any variant URL and prefers a stored cutout, generating
  // one from the original when none exists yet — so any of these URLs works.
  return photo.cutoutUrl || photo.thumbnailUrl || photo.url || null;
}
