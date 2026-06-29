import { useCallback, useMemo, useRef } from 'react';
import { uploadGarmentPhoto } from '../../api/client';
import { selectQueueItemsToStart, type UploadQueueItem } from './wardrobeUpload';

/**
 * Drives background removal as soon as garment photos are selected, before the
 * user submits the queue. Each `queued` item is uploaded (the upload endpoint
 * runs rembg server-side) up to a small concurrency limit; the processed result
 * is stored back on the item so submit only has to create the garment.
 */
const MAX_CONCURRENT_UPLOADS = 3;

type QueueUpdater = (updater: (items: UploadQueueItem[]) => UploadQueueItem[]) => void;

export interface EagerGarmentUploads {
  /** Start processing any `queued` items that fit within the concurrency limit. */
  start: (items: UploadQueueItem[]) => void;
  /** Abort an in-flight upload (used when an item is removed from the queue). */
  abort: (itemId: string) => void;
}

export function useEagerGarmentUploads(setQueue: QueueUpdater): EagerGarmentUploads {
  // The controller map doubles as the in-flight guard. It is read synchronously,
  // so StrictMode's double-invoked effects cannot start the same upload twice.
  const controllersRef = useRef<Map<string, AbortController>>(new Map());

  const start = useCallback(
    (items: UploadQueueItem[]) => {
      const inFlightIds = new Set(controllersRef.current.keys());
      const toStart = selectQueueItemsToStart(items, MAX_CONCURRENT_UPLOADS, inFlightIds);
      if (toStart.length === 0) {
        return;
      }

      const startingIds = new Set(toStart.map((item) => item.id));
      setQueue((current) =>
        current.map((item) =>
          startingIds.has(item.id) ? { ...item, status: 'processing', error: null } : item
        )
      );

      for (const item of toStart) {
        const controller = new AbortController();
        controllersRef.current.set(item.id, controller);

        uploadGarmentPhoto(item.file, controller.signal)
          .then((uploadedPhoto) => {
            controllersRef.current.delete(item.id);
            setQueue((current) =>
              current.map((queued) =>
                queued.id === item.id
                  ? { ...queued, status: 'processed', uploadedPhoto, error: null }
                  : queued
              )
            );
          })
          .catch((error: unknown) => {
            controllersRef.current.delete(item.id);
            if (controller.signal.aborted) {
              return;
            }

            setQueue((current) =>
              current.map((queued) =>
                queued.id === item.id
                  ? { ...queued, status: 'failed', error: uploadErrorMessage(error) }
                  : queued
              )
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

function uploadErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Background removal failed. Try again.';
}
