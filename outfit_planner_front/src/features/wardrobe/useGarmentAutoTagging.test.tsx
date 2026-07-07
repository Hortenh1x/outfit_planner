import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi, type Mock } from 'vitest';
import type { GarmentAutoTagResponse } from '../../api/client';
import { classifyGarmentPhoto } from '../../api/client';
import { useGarmentAutoTagging } from './useGarmentAutoTagging';
import { createUploadQueueItems, type UploadQueueItem } from './wardrobeUpload';

vi.mock('../../api/client', () => ({ classifyGarmentPhoto: vi.fn() }));

const classifyMock = classifyGarmentPhoto as unknown as Mock;

function suggestions(overrides: Partial<GarmentAutoTagResponse> = {}): GarmentAutoTagResponse {
  return {
    isAvailable: true,
    provider: 'test',
    category: null,
    categoryConfidence: 0,
    colors: [],
    seasons: [],
    tags: [],
    ...overrides
  };
}

function processedRow(): UploadQueueItem {
  const [item] = createUploadQueueItems(
    [new File(['x'], 'plain.png', { type: 'image/png' })],
    { category: 'Top', color: '', season: [], existingTags: [] }
  );
  return {
    ...item,
    status: 'processed',
    autoTagStatus: 'idle',
    uploadedPhoto: { fileName: 'f', contentType: 'image/png', length: 1, url: 'https://app.test/cutout.png' }
  };
}

afterEach(() => {
  classifyMock.mockReset();
});

describe('useGarmentAutoTagging', () => {
  it('prefills a processed row from the mocked classification', async () => {
    classifyMock.mockResolvedValue(
      suggestions({ category: 'Dress', colors: [{ name: 'navy', hex: '#1f2a44', confidence: 0.6 }] })
    );
    let queue: UploadQueueItem[] = [processedRow()];
    const setQueue = (updater: (items: UploadQueueItem[]) => UploadQueueItem[]) => {
      queue = updater(queue);
    };
    const { result } = renderHook(() => useGarmentAutoTagging(setQueue));

    act(() => result.current.start(queue, ['work']));
    await waitFor(() => expect(queue[0].autoTagStatus).toBe('done'));

    expect(classifyMock).toHaveBeenCalledWith('https://app.test/cutout.png', ['work'], expect.anything());
    expect(queue[0].category).toBe('Dress');
    expect(queue[0].primaryColor).toBe('navy');
  });

  it('does not overwrite a field the user edits while classification is in flight', async () => {
    let resolveClassify: (value: GarmentAutoTagResponse) => void = () => {};
    classifyMock.mockReturnValue(
      new Promise<GarmentAutoTagResponse>((resolve) => {
        resolveClassify = resolve;
      })
    );
    let queue: UploadQueueItem[] = [processedRow()];
    const setQueue = (updater: (items: UploadQueueItem[]) => UploadQueueItem[]) => {
      queue = updater(queue);
    };
    const { result } = renderHook(() => useGarmentAutoTagging(setQueue));

    act(() => result.current.start(queue, []));
    // The user picks a category before the model responds.
    queue = queue.map((item) => ({ ...item, category: 'Bottom', categoryEdited: true }));

    await act(async () => {
      resolveClassify(suggestions({ category: 'Dress' }));
    });
    await waitFor(() => expect(queue[0].autoTagStatus).toBe('done'));

    expect(queue[0].category).toBe('Bottom');
  });
});
