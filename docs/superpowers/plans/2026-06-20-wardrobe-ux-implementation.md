# Wardrobe UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Wardrobe page as a frontend-first editorial wardrobe manager with filters, search, edit, duplicate, archive, favorite, bulk upload, drag-and-drop, mobile camera upload, clean photo checklist, auto-tag suggestions, photo warnings, and tests.

**Architecture:** Keep backend contracts unchanged and compose the new UX from the existing frontend API wrapper. Move wardrobe logic into focused files under `src/features/wardrobe/`, keep `src/routes/WardrobePage.tsx` as the route orchestrator, and isolate the new Obra/Crimson-inspired visual language with new class names and CSS files instead of expanding legacy claymorphism styles.

**Tech Stack:** React, TypeScript, Vite, React Router, TanStack Query, Vitest, Testing Library, user-event, lucide-react, existing OpenAPI-derived DTO types, existing same-origin `/api` client.

---

## References

- Spec: `docs/superpowers/specs/2026-06-20-wardrobe-ux-design.md`
- Existing route: `outfit_planner_front/src/routes/WardrobePage.tsx`
- Existing route tests: `outfit_planner_front/src/routes/WardrobePage.test.tsx`
- Existing API wrapper: `outfit_planner_front/src/api/client.ts`
- Existing upload validation: `outfit_planner_front/src/features/uploads/imageFile.ts`
- Existing category constants: `outfit_planner_front/src/features/outfits/outfitUtils.ts`
- Visual direction: user-provided Obra Studio dark and Crimson Plinth light screenshots.

## File Map

Create:

- `outfit_planner_front/src/app/editorialShell.css`: app shell, left navigation, bottom navigation, and light/dark editorial theme variables.
- `outfit_planner_front/src/features/wardrobe/wardrobe.css`: Wardrobe route layout, catalog, controls, cards, right rail, upload queue, empty states, and responsive rules.
- `outfit_planner_front/src/features/wardrobe/wardrobeFilters.ts`: filter state, default filters, API filter conversion, local tag filtering, and duplicate payload helper.
- `outfit_planner_front/src/features/wardrobe/wardrobeFilters.test.ts`: unit tests for filters and duplicate payload behavior.
- `outfit_planner_front/src/features/wardrobe/wardrobeUpload.ts`: upload queue item creation, file validation bridge, clean checklist labels, tag suggestions, and photo warning heuristics.
- `outfit_planner_front/src/features/wardrobe/wardrobeUpload.test.ts`: unit tests for queue, suggestions, validation, and warnings.
- `outfit_planner_front/src/features/wardrobe/wardrobeMutations.ts`: hooks that wrap create/update/delete/upload mutations and invalidate wardrobe queries consistently.
- `outfit_planner_front/src/features/wardrobe/WardrobeFilters.tsx`: search, category, color, season, tag, favorite, archived, and sort controls.
- `outfit_planner_front/src/features/wardrobe/GarmentCard.tsx`: editorial product card with favorite, edit, duplicate, archive, and delete actions.
- `outfit_planner_front/src/features/wardrobe/GarmentEditor.tsx`: right rail edit form for an existing garment.
- `outfit_planner_front/src/features/wardrobe/WardrobeUploadPanel.tsx`: right rail add/upload mode with clean photo checklist, drop zone, file inputs, and queue submit.
- `outfit_planner_front/src/features/wardrobe/UploadQueue.tsx`: editable per-file upload rows with warnings and tag suggestions.
- `outfit_planner_front/src/app/AppShell.test.tsx`: shell smoke test for the new editorial frame.

Modify:

- `outfit_planner_front/src/app/AppShell.tsx`: switch private app shell from clay classes and blobs to the editorial frame while preserving route outlet, auth actions, theme toggle, and mobile navigation.
- `outfit_planner_front/src/routes/WardrobePage.tsx`: replace the current monolithic wardrobe UI with the new orchestrator that uses feature components and existing API operations.
- `outfit_planner_front/src/routes/WardrobePage.test.tsx`: broaden coverage from category/delete only to the full Wardrobe UX behavior set.
- `outfit_planner_front/src/styles.css`: leave legacy clay rules in place for old surfaces, but remove only imports/usages that become unreachable if TypeScript or linting reports them. Prefer not to churn this file for Wardrobe-specific visuals.
- `README.md`: add a short note that Wardrobe now uses the editorial Obra/Crimson visual system and supports bulk upload, edit, duplicate, archive, favorite, filters, and photo guidance.
- `agents.md`: update only if implementation discovers a durable rule not already captured by the spec commit.

Generated or local artifacts not to commit:

- `outfit_planner_front/.generated/`
- `outfit_planner_front/src/api/generated/schema.ts`
- `.superpowers/`
- screenshots, uploads, test results, and browser traces unless explicitly needed for a test fixture.

## Task 1: Wardrobe Helper Layer

**Files:**

- Create: `outfit_planner_front/src/features/wardrobe/wardrobeFilters.ts`
- Create: `outfit_planner_front/src/features/wardrobe/wardrobeFilters.test.ts`
- Create: `outfit_planner_front/src/features/wardrobe/wardrobeUpload.ts`
- Create: `outfit_planner_front/src/features/wardrobe/wardrobeUpload.test.ts`

- [ ] **Step 1: Add failing filter helper tests**

Create `outfit_planner_front/src/features/wardrobe/wardrobeFilters.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import type { GarmentItem } from '../../types';
import {
  defaultWardrobeFilters,
  duplicateGarmentInput,
  filterGarmentsByLocalTags,
  toGarmentFilters
} from './wardrobeFilters';

const baseGarment: GarmentItem = {
  id: 'garment-1',
  userId: 'user-1',
  name: 'Black silk cami',
  category: 'Top',
  bodyZone: 'Torso',
  imageUrl: '/uploads/black-silk-cami.png',
  thumbnailUrl: '/uploads/black-silk-cami-thumb.png',
  tags: ['silk', 'evening'],
  primaryColor: 'black',
  secondaryColors: ['cream'],
  material: 'silk',
  brand: 'studio',
  size: 'S',
  season: ['summer'],
  weatherMinTemp: 18,
  weatherMaxTemp: 30,
  occasion: ['date night'],
  formalityScore: 4,
  warmthScore: 1,
  comfortScore: 4,
  isFavorite: true,
  isArchived: false,
  lastWornAt: '2026-06-01T12:00:00Z',
  laundryStatus: 'clean',
  createdAt: '2026-06-01T12:00:00Z'
};

describe('wardrobeFilters', () => {
  it('defaults to recent unarchived garments', () => {
    expect(defaultWardrobeFilters).toEqual({
      q: '',
      category: 'All',
      color: '',
      season: '',
      tag: '',
      favorite: false,
      archived: false,
      sort: 'recent'
    });
    expect(toGarmentFilters(defaultWardrobeFilters)).toEqual({ archived: false, sort: 'recent' });
  });

  it('converts active UI filters into API garment filters', () => {
    expect(toGarmentFilters({
      q: 'silk',
      category: 'Top',
      color: 'black',
      season: 'summer',
      tag: 'evening',
      favorite: true,
      archived: true,
      sort: 'name'
    })).toEqual({
      q: 'silk',
      category: 'Top',
      color: 'black',
      season: 'summer',
      favorite: true,
      archived: true,
      sort: 'name'
    });
  });

  it('filters locally by tag when a tag chip is active', () => {
    const garments = [
      baseGarment,
      { ...baseGarment, id: 'garment-2', name: 'Trench coat', tags: ['rain'], primaryColor: 'beige' }
    ];

    expect(filterGarmentsByLocalTags(garments, 'evening')).toEqual([baseGarment]);
    expect(filterGarmentsByLocalTags(garments, '')).toEqual(garments);
  });

  it('builds a safe duplicate payload without worn state', () => {
    expect(duplicateGarmentInput(baseGarment)).toEqual({
      name: 'Black silk cami copy',
      category: 'Top',
      imageUrl: '/uploads/black-silk-cami.png',
      thumbnailUrl: '/uploads/black-silk-cami-thumb.png',
      tags: ['silk', 'evening'],
      primaryColor: 'black',
      secondaryColors: ['cream'],
      material: 'silk',
      brand: 'studio',
      size: 'S',
      season: ['summer'],
      weatherMinTemp: 18,
      weatherMaxTemp: 30,
      occasion: ['date night'],
      formalityScore: 4,
      warmthScore: 1,
      comfortScore: 4,
      isFavorite: false,
      isArchived: false,
      laundryStatus: 'clean'
    });
  });
});
```

- [ ] **Step 2: Add failing upload helper tests**

Create `outfit_planner_front/src/features/wardrobe/wardrobeUpload.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import {
  cleanPhotoChecklist,
  createUploadQueueItems,
  getPhotoQualityWarnings,
  suggestTagsForUpload,
  updateUploadQueueItem
} from './wardrobeUpload';

describe('wardrobeUpload', () => {
  it('exposes the clean photo checklist copy required before upload', () => {
    expect(cleanPhotoChecklist).toEqual([
      'Front view',
      'Good lighting',
      'No background clutter'
    ]);
  });

  it('creates editable queue rows from multiple supported image files', () => {
    const files = [
      new File(['shirt'], 'black-silk-cami.png', { type: 'image/png' }),
      new File(['coat'], 'wool-blazer.webp', { type: 'image/webp' })
    ];

    const rows = createUploadQueueItems(files, {
      category: 'Top',
      color: 'black',
      season: ['summer'],
      existingTags: ['favorite']
    });

    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({
      file: files[0],
      name: 'Black silk cami',
      category: 'Top',
      tags: ['black', 'silk', 'cami', 'top', 'summer', 'favorite'],
      primaryColor: 'black',
      season: ['summer'],
      status: 'ready',
      validationError: null
    });
    expect(rows[1].name).toBe('Wool blazer');
  });

  it('keeps unsupported files in the queue with a validation error', () => {
    const rows = createUploadQueueItems([
      new File(['notes'], 'notes.txt', { type: 'text/plain' })
    ], { category: 'Accessory', color: '', season: [], existingTags: [] });

    expect(rows[0]).toMatchObject({
      name: 'Notes',
      category: 'Accessory',
      status: 'invalid',
      validationError: 'Upload a JPG, PNG, or WebP image.'
    });
  });

  it('suggests tags from filename category color season and existing tags', () => {
    expect(suggestTagsForUpload({
      fileName: 'cream-linen-shirt.JPG',
      category: 'Top',
      color: 'cream',
      season: ['spring', 'summer'],
      existingTags: ['work']
    })).toEqual(['cream', 'linen', 'shirt', 'top', 'spring', 'summer', 'work']);
  });

  it('adds advisory photo warnings for weak upload candidates', () => {
    const warnings = getPhotoQualityWarnings(
      new File(['x'], 'IMG_0001.png', { type: 'image/png' }),
      { width: 320, height: 1200 }
    );

    expect(warnings).toContain('Image dimensions are small; use a sharper front-view photo if possible.');
    expect(warnings).toContain('The photo is very tall or wide; crop around the garment before uploading.');
    expect(warnings).toContain('The filename is generic; confirm the generated name and tags before saving.');
    expect(warnings).toContain('The file is tiny; confirm the photo is not a placeholder or compressed preview.');
  });

  it('updates a queue row without mutating the existing row', () => {
    const [row] = createUploadQueueItems([
      new File(['shirt'], 'black-shirt.png', { type: 'image/png' })
    ], { category: 'Top', color: 'black', season: [], existingTags: [] });

    const updated = updateUploadQueueItem(row, { name: 'Black evening shirt', tags: ['black', 'evening'] });

    expect(updated).toMatchObject({ name: 'Black evening shirt', tags: ['black', 'evening'] });
    expect(row.name).toBe('Black shirt');
  });
});
```

- [ ] **Step 3: Run helper tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/wardrobe/wardrobeFilters.test.ts src/features/wardrobe/wardrobeUpload.test.ts
cd ..
```

Expected: fails because `wardrobeFilters.ts` and `wardrobeUpload.ts` do not exist.

- [ ] **Step 4: Implement filter helpers**

Create `outfit_planner_front/src/features/wardrobe/wardrobeFilters.ts`:

```ts
import type { GarmentFilters, GarmentMetadataInput } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';

export type WardrobeCategoryFilter = GarmentCategory | 'All';

export interface WardrobeFilterState {
  q: string;
  category: WardrobeCategoryFilter;
  color: string;
  season: string;
  tag: string;
  favorite: boolean;
  archived: boolean;
  sort: NonNullable<GarmentFilters['sort']>;
}

export const defaultWardrobeFilters: WardrobeFilterState = {
  q: '',
  category: 'All',
  color: '',
  season: '',
  tag: '',
  favorite: false,
  archived: false,
  sort: 'recent'
};

export function toGarmentFilters(filters: WardrobeFilterState): GarmentFilters {
  return {
    ...(filters.q.trim() ? { q: filters.q.trim() } : {}),
    ...(filters.category !== 'All' ? { category: filters.category } : {}),
    ...(filters.color.trim() ? { color: filters.color.trim() } : {}),
    ...(filters.season.trim() ? { season: filters.season.trim() } : {}),
    ...(filters.favorite ? { favorite: true } : {}),
    archived: filters.archived,
    sort: filters.sort
  };
}

export function filterGarmentsByLocalTags(garments: GarmentItem[], tag: string): GarmentItem[] {
  const normalizedTag = normalizeTag(tag);
  if (!normalizedTag) {
    return garments;
  }

  return garments.filter((garment) => garment.tags.some((garmentTag) => normalizeTag(garmentTag) === normalizedTag));
}

export function duplicateGarmentInput(garment: GarmentItem): {
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  thumbnailUrl?: string;
  tags: string[];
} & GarmentMetadataInput {
  return {
    name: `${garment.name} copy`,
    category: garment.category,
    imageUrl: garment.imageUrl,
    thumbnailUrl: garment.thumbnailUrl,
    tags: [...garment.tags],
    primaryColor: garment.primaryColor,
    secondaryColors: [...(garment.secondaryColors ?? [])],
    material: garment.material,
    brand: garment.brand,
    size: garment.size,
    season: [...(garment.season ?? [])],
    weatherMinTemp: garment.weatherMinTemp,
    weatherMaxTemp: garment.weatherMaxTemp,
    occasion: [...(garment.occasion ?? [])],
    formalityScore: garment.formalityScore,
    warmthScore: garment.warmthScore,
    comfortScore: garment.comfortScore,
    isFavorite: false,
    isArchived: false,
    laundryStatus: garment.laundryStatus
  };
}

function normalizeTag(tag: string): string {
  return tag.trim().toLowerCase();
}
```

- [ ] **Step 5: Implement upload helpers**

Create `outfit_planner_front/src/features/wardrobe/wardrobeUpload.ts`:

```ts
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

  return tokens.map((token) => token.charAt(0).toUpperCase() + token.slice(1)).join(' ');
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
```

- [ ] **Step 6: Run helper tests and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/wardrobe/wardrobeFilters.test.ts src/features/wardrobe/wardrobeUpload.test.ts
cd ..
```

Expected: both test files pass.

- [ ] **Step 7: Commit helper layer**

Run:

```powershell
git add outfit_planner_front\src\features\wardrobe\wardrobeFilters.ts outfit_planner_front\src\features\wardrobe\wardrobeFilters.test.ts outfit_planner_front\src\features\wardrobe\wardrobeUpload.ts outfit_planner_front\src\features\wardrobe\wardrobeUpload.test.ts
git commit -m "Add wardrobe UX helpers"
```

## Task 2: Editorial App Shell

**Files:**

- Create: `outfit_planner_front/src/app/AppShell.test.tsx`
- Create: `outfit_planner_front/src/app/editorialShell.css`
- Modify: `outfit_planner_front/src/app/AppShell.tsx`

- [ ] **Step 1: Add failing shell test**

Create `outfit_planner_front/src/app/AppShell.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

function renderShell() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);
    if (url.endsWith('/auth/me')) {
      return jsonResponse({ user: { id: 'user-1', email: 'sienna@example.test', displayName: 'Sienna Studio' }, expiresAt: '2026-07-20T12:00:00Z' });
    }

    if (url.endsWith('/auth/providers')) {
      return jsonResponse([]);
    }

    return jsonResponse({});
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/wardrobe']}>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="/wardrobe" element={<h1>Wardrobe route</h1>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('AppShell editorial frame', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('renders private routes inside the editorial shell without clay ambient blobs', async () => {
    const { container } = renderShell();

    expect(await screen.findByRole('heading', { name: /wardrobe route/i })).toBeInTheDocument();
    expect(container.querySelector('.editorial-shell')).toBeInTheDocument();
    expect(container.querySelector('.editorial-sidebar')).toBeInTheDocument();
    expect(container.querySelector('.clay-ambient')).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: /primary navigation/i })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: /mobile primary navigation/i })).toBeInTheDocument();
  });

  it('maps the theme toggle to the editorial light and dark themes', async () => {
    const { container } = renderShell();

    expect(container.querySelector('.editorial-shell')).toHaveAttribute('data-theme', 'light');
    await userEvent.click(await screen.findByRole('button', { name: /switch to dark theme/i }));

    expect(container.querySelector('.editorial-shell')).toHaveAttribute('data-theme', 'dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

- [ ] **Step 2: Run shell test and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/app/AppShell.test.tsx
cd ..
```

Expected: fails because `.editorial-shell` is missing and `.clay-ambient` is still rendered.

- [ ] **Step 3: Update AppShell markup**

Modify `outfit_planner_front/src/app/AppShell.tsx` so it no longer imports or renders `ClayBlobs`, imports `./editorialShell.css`, and uses editorial class names:

```tsx
import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, LogOut, Shirt, Sparkles, Upload, Wand2 } from 'lucide-react';
import { getAuthProviders, logout } from '../api/client';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import './editorialShell.css';

export function AppShell() {
  const queryClient = useQueryClient();
  const [theme, setTheme] = useState<ThemeMode>(() => {
    const storedTheme = localStorage.getItem('outfit-planner-theme');
    return storedTheme === 'dark' ? 'dark' : 'light';
  });
  const sessionQuery = useAuthSession();
  const authProvidersQuery = useQuery({ queryKey: ['auth-providers'], queryFn: getAuthProviders, retry: 1 });
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(authSessionQueryKey, null);
      void queryClient.invalidateQueries();
    }
  });

  useEffect(() => {
    localStorage.setItem('outfit-planner-theme', theme);
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <div className="editorial-shell" data-theme={theme}>
      <aside className="editorial-sidebar">
        <Link to="/wardrobe" className="editorial-brand">
          <Shirt size={19} />
          <span>Outfit Planner</span>
        </Link>
        <PrimaryNavigation />
        <div className="editorial-account" aria-label="Signed in account">
          <span className="editorial-avatar">{sessionQuery.data?.user.displayName?.charAt(0).toUpperCase() ?? 'S'}</span>
          <span>
            <small>Signed in as</small>
            <strong>{sessionQuery.data?.user.email ?? sessionQuery.data?.user.displayName ?? 'Local session'}</strong>
          </span>
        </div>
        <button type="button" className="editorial-nav-button" disabled={logoutMutation.isPending} onClick={() => logoutMutation.mutate()}>
          <LogOut size={17} />
          <span>{logoutMutation.isPending ? 'Signing out' : 'Sign out'}</span>
        </button>
        <div className="editorial-theme-row">
          <Sparkles size={16} />
          <span>Theme</span>
          <ThemeToggle theme={theme} onChange={setTheme} />
        </div>
      </aside>
      <main className="editorial-main-panel">
        <Outlet context={{ providers: authProvidersQuery.data ?? [] }} />
      </main>
      <nav className="editorial-bottom-navigation" aria-label="Mobile primary navigation">
        <PrimaryNavigation compact />
      </nav>
    </div>
  );
}

function PrimaryNavigation({ compact = false }: { compact?: boolean }) {
  return (
    <nav aria-label={compact ? 'Mobile workspace navigation' : 'Primary navigation'} className={compact ? 'editorial-nav compact' : 'editorial-nav'}>
      <NavLink to="/wardrobe">
        <Upload size={18} />
        <span>Wardrobe</span>
      </NavLink>
      <NavLink to="/builder">
        <Wand2 size={18} />
        <span>Builder</span>
      </NavLink>
      <NavLink to="/calendar">
        <CalendarDays size={18} />
        <span>Calendar</span>
      </NavLink>
    </nav>
  );
}
```

- [ ] **Step 4: Add editorial shell CSS**

Create `outfit_planner_front/src/app/editorialShell.css`:

```css
@import url("https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;700;800&family=Instrument+Serif:ital@0;1&display=swap");

:root {
  --editorial-font-body: "DM Sans", system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --editorial-font-display: "Instrument Serif", Georgia, serif;
  --editorial-crimson: #d5092f;
  --editorial-crimson-deep: #a80424;
}

:root[data-theme="light"] {
  --editorial-canvas: #f7efe4;
  --editorial-panel: #fff8ee;
  --editorial-panel-soft: #fbf1e5;
  --editorial-ink: #181511;
  --editorial-muted: #6f655a;
  --editorial-faint: #998d7e;
  --editorial-line: rgba(41, 31, 22, 0.11);
  --editorial-line-strong: rgba(41, 31, 22, 0.18);
  --editorial-shadow: 0 18px 44px rgba(71, 52, 32, 0.1);
}

:root[data-theme="dark"] {
  --editorial-canvas: #11100d;
  --editorial-panel: #181612;
  --editorial-panel-soft: #211c16;
  --editorial-ink: #f5dec0;
  --editorial-muted: #b7a58e;
  --editorial-faint: #8e7c67;
  --editorial-line: rgba(245, 222, 192, 0.11);
  --editorial-line-strong: rgba(245, 222, 192, 0.2);
  --editorial-shadow: 0 18px 44px rgba(0, 0, 0, 0.28);
}

body {
  background: var(--editorial-canvas);
}

.editorial-shell {
  background: var(--editorial-canvas);
  color: var(--editorial-ink);
  display: grid;
  font-family: var(--editorial-font-body);
  grid-template-columns: 220px minmax(0, 1fr);
  min-height: 100vh;
}

.editorial-sidebar {
  background: var(--editorial-panel);
  border-right: 1px solid var(--editorial-line);
  display: grid;
  gap: 1.1rem;
  grid-template-rows: auto auto 1fr auto auto;
  padding: 1.35rem 1rem;
}

.editorial-brand {
  align-items: center;
  display: inline-flex;
  font-family: var(--editorial-font-display);
  font-size: 1.35rem;
  gap: 0.55rem;
  text-decoration: none;
}

.editorial-nav {
  display: grid;
  gap: 0.35rem;
}

.editorial-nav a,
.editorial-nav-button,
.editorial-theme-row {
  align-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 8px;
  color: var(--editorial-muted);
  display: flex;
  font-size: 0.9rem;
  font-weight: 800;
  gap: 0.7rem;
  min-height: 2.55rem;
  padding: 0 0.75rem;
  text-decoration: none;
}

.editorial-nav a.active {
  background: color-mix(in srgb, var(--editorial-crimson) 10%, transparent);
  border-color: var(--editorial-line);
  color: var(--editorial-crimson);
}

.editorial-account {
  align-items: center;
  align-self: end;
  background: var(--editorial-panel-soft);
  border: 1px solid var(--editorial-line);
  border-radius: 12px;
  display: grid;
  gap: 0.65rem;
  grid-template-columns: auto minmax(0, 1fr);
  padding: 0.7rem;
}

.editorial-avatar {
  align-items: center;
  background: #1a1208;
  border-radius: 999px;
  color: #f5dec0;
  display: inline-flex;
  font-weight: 900;
  height: 2rem;
  justify-content: center;
  width: 2rem;
}

.editorial-account small {
  color: var(--editorial-faint);
  display: block;
  font-size: 0.62rem;
  font-weight: 900;
  letter-spacing: 0;
  text-transform: uppercase;
}

.editorial-account strong {
  display: block;
  font-size: 0.75rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.editorial-nav-button {
  justify-content: flex-start;
}

.editorial-theme-row {
  border-color: var(--editorial-line);
  justify-content: space-between;
}

.editorial-theme-row .theme-toggle {
  background: transparent;
  border: 0;
  color: var(--editorial-ink);
  min-height: 2rem;
  padding: 0;
}

.editorial-main-panel {
  min-width: 0;
}

.editorial-bottom-navigation {
  display: none;
}

@media (max-width: 860px) {
  .editorial-shell {
    display: block;
    padding-bottom: 76px;
  }

  .editorial-sidebar {
    display: none;
  }

  .editorial-bottom-navigation {
    background: color-mix(in srgb, var(--editorial-panel) 94%, transparent);
    border: 1px solid var(--editorial-line);
    border-radius: 18px;
    bottom: 10px;
    box-shadow: var(--editorial-shadow);
    display: block;
    left: 10px;
    padding: 0.45rem;
    position: fixed;
    right: 10px;
    z-index: 30;
  }

  .editorial-nav.compact {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .editorial-nav.compact a {
    justify-content: center;
    min-height: 3rem;
  }
}
```

- [ ] **Step 5: Run shell test and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/app/AppShell.test.tsx
cd ..
```

Expected: shell test passes.

- [ ] **Step 6: Commit shell frame**

Run:

```powershell
git add outfit_planner_front\src\app\AppShell.tsx outfit_planner_front\src\app\AppShell.test.tsx outfit_planner_front\src\app\editorialShell.css
git commit -m "Add editorial app shell frame"
```

## Task 3: Wardrobe Component Surfaces

**Files:**

- Create: `outfit_planner_front/src/features/wardrobe/GarmentCard.tsx`
- Create: `outfit_planner_front/src/features/wardrobe/GarmentEditor.tsx`
- Create: `outfit_planner_front/src/features/wardrobe/UploadQueue.tsx`
- Create: `outfit_planner_front/src/features/wardrobe/WardrobeFilters.tsx`
- Create: `outfit_planner_front/src/features/wardrobe/WardrobeUploadPanel.tsx`

- [ ] **Step 1: Create `GarmentCard.tsx`**

Create `outfit_planner_front/src/features/wardrobe/GarmentCard.tsx`:

```tsx
import { Archive, Copy, Heart, MoreHorizontal, Pencil, Trash2 } from 'lucide-react';
import type { GarmentItem } from '../../types';

export function GarmentCard({
  garment,
  pendingAction,
  onArchive,
  onDelete,
  onDuplicate,
  onEdit,
  onFavorite
}: {
  garment: GarmentItem;
  pendingAction?: string;
  onArchive: (garment: GarmentItem) => void;
  onDelete: (garment: GarmentItem) => void;
  onDuplicate: (garment: GarmentItem) => void;
  onEdit: (garment: GarmentItem) => void;
  onFavorite: (garment: GarmentItem) => void;
}) {
  const disabled = Boolean(pendingAction);

  return (
    <article className={garment.isArchived ? 'wardrobe-card archived' : 'wardrobe-card'}>
      <div className="wardrobe-card-image">
        <img src={garment.thumbnailUrl || garment.imageUrl} alt={garment.name} />
        <button
          type="button"
          className={garment.isFavorite ? 'wardrobe-icon-button active' : 'wardrobe-icon-button'}
          aria-label={`${garment.isFavorite ? 'Unfavorite' : 'Favorite'} ${garment.name}`}
          disabled={disabled}
          onClick={() => onFavorite(garment)}
        >
          <Heart size={16} fill={garment.isFavorite ? 'currentColor' : 'none'} />
        </button>
      </div>
      <div className="wardrobe-card-body">
        <div>
          <h3>{garment.name}</h3>
          <p>{garment.category}</p>
        </div>
        <div className="wardrobe-card-actions" aria-label={`Actions for ${garment.name}`}>
          <button type="button" aria-label={`Edit ${garment.name}`} disabled={disabled} onClick={() => onEdit(garment)}>
            <Pencil size={15} />
          </button>
          <button type="button" aria-label={`Duplicate ${garment.name}`} disabled={disabled} onClick={() => onDuplicate(garment)}>
            <Copy size={15} />
          </button>
          <button type="button" aria-label={`${garment.isArchived ? 'Restore' : 'Archive'} ${garment.name}`} disabled={disabled} onClick={() => onArchive(garment)}>
            <Archive size={15} />
          </button>
          <button type="button" aria-label={`Delete ${garment.name}`} disabled={disabled} onClick={() => onDelete(garment)}>
            <Trash2 size={15} />
          </button>
          <MoreHorizontal size={15} aria-hidden="true" />
        </div>
      </div>
    </article>
  );
}
```

- [ ] **Step 2: Create `WardrobeFilters.tsx`**

Create `outfit_planner_front/src/features/wardrobe/WardrobeFilters.tsx`:

```tsx
import { Grid2X2, List, Search, SlidersHorizontal, X } from 'lucide-react';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import type { WardrobeFilterState } from './wardrobeFilters';

const colorOptions = ['', 'black', 'cream', 'brown', 'blue', 'red', 'green'];
const seasonOptions = ['', 'spring', 'summer', 'fall', 'winter'];

export function WardrobeFilters({
  filters,
  itemCount,
  onChange,
  onReset
}: {
  filters: WardrobeFilterState;
  itemCount: number;
  onChange: (filters: WardrobeFilterState) => void;
  onReset: () => void;
}) {
  return (
    <section className="wardrobe-controls" aria-label="Wardrobe filters">
      <div className="wardrobe-search-row">
        <label className="wardrobe-search">
          <Search size={16} />
          <span className="sr-only">Search wardrobe</span>
          <input
            value={filters.q}
            placeholder="Search wardrobe"
            onChange={(event) => onChange({ ...filters, q: event.target.value })}
          />
        </label>
        <label>
          <span className="sr-only">Category filter</span>
          <select value={filters.category} onChange={(event) => onChange({ ...filters, category: event.target.value as WardrobeFilterState['category'] })}>
            <option value="All">All categories</option>
            {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
          </select>
        </label>
        <div className="wardrobe-view-buttons" aria-label="Catalog view">
          <button type="button" aria-label="Grid view"><Grid2X2 size={16} /></button>
          <button type="button" aria-label="List view"><List size={16} /></button>
        </div>
      </div>
      <div className="wardrobe-tab-row" role="tablist" aria-label="Garment categories">
        <button type="button" role="tab" aria-selected={filters.category === 'All'} onClick={() => onChange({ ...filters, category: 'All' })}>All</button>
        {GARMENT_CATEGORIES.map((category) => (
          <button key={category} type="button" role="tab" aria-selected={filters.category === category} onClick={() => onChange({ ...filters, category })}>
            {category}
          </button>
        ))}
        <span className="wardrobe-item-count">{itemCount} items</span>
      </div>
      <div className="wardrobe-filter-row">
        <SlidersHorizontal size={16} aria-hidden="true" />
        <label>
          <span>Color</span>
          <select value={filters.color} onChange={(event) => onChange({ ...filters, color: event.target.value })}>
            {colorOptions.map((color) => <option key={color || 'any'} value={color}>{color || 'Any color'}</option>)}
          </select>
        </label>
        <label>
          <span>Season</span>
          <select value={filters.season} onChange={(event) => onChange({ ...filters, season: event.target.value })}>
            {seasonOptions.map((season) => <option key={season || 'any'} value={season}>{season || 'Any season'}</option>)}
          </select>
        </label>
        <label>
          <span>Tags</span>
          <input value={filters.tag} placeholder="silk, office, rain" onChange={(event) => onChange({ ...filters, tag: event.target.value })} />
        </label>
        <label className="wardrobe-check">
          <input type="checkbox" checked={filters.favorite} onChange={(event) => onChange({ ...filters, favorite: event.target.checked })} />
          Favorites
        </label>
        <label className="wardrobe-check">
          <input type="checkbox" checked={filters.archived} onChange={(event) => onChange({ ...filters, archived: event.target.checked })} />
          Archived
        </label>
        <label>
          <span>Sort</span>
          <select value={filters.sort} onChange={(event) => onChange({ ...filters, sort: event.target.value as WardrobeFilterState['sort'] })}>
            <option value="recent">Recent</option>
            <option value="oldest">Oldest</option>
            <option value="name">Name</option>
            <option value="category">Category</option>
          </select>
        </label>
        <button type="button" className="wardrobe-ghost-button" onClick={onReset}>
          <X size={15} />
          Reset
        </button>
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Create `UploadQueue.tsx`**

Create `outfit_planner_front/src/features/wardrobe/UploadQueue.tsx`:

```tsx
import type { GarmentCategory } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';
import type { UploadQueueItem } from './wardrobeUpload';

export function UploadQueue({
  items,
  onAcceptTag,
  onChangeItem,
  onRemove
}: {
  items: UploadQueueItem[];
  onAcceptTag: (itemId: string, tag: string) => void;
  onChangeItem: (itemId: string, updates: Partial<Pick<UploadQueueItem, 'name' | 'category' | 'tags' | 'primaryColor' | 'season'>>) => void;
  onRemove: (itemId: string) => void;
}) {
  if (items.length === 0) {
    return <p className="wardrobe-rail-note">Drop several photos or use the camera input to build an upload queue.</p>;
  }

  return (
    <div className="upload-queue" aria-label="Upload queue">
      {items.map((item) => (
        <article className={item.status === 'invalid' ? 'upload-queue-row invalid' : 'upload-queue-row'} key={item.id}>
          <div className="upload-queue-heading">
            <strong>{item.file.name}</strong>
            <button type="button" onClick={() => onRemove(item.id)}>Remove</button>
          </div>
          <label>
            <span>Name</span>
            <input value={item.name} onChange={(event) => onChangeItem(item.id, { name: event.target.value })} />
          </label>
          <label>
            <span>Type</span>
            <select value={item.category} onChange={(event) => onChangeItem(item.id, { category: event.target.value as GarmentCategory })}>
              {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
            </select>
          </label>
          <label>
            <span>Color</span>
            <input value={item.primaryColor} onChange={(event) => onChangeItem(item.id, { primaryColor: event.target.value })} />
          </label>
          <label>
            <span>Tags</span>
            <input value={item.tags.join(', ')} onChange={(event) => onChangeItem(item.id, { tags: event.target.value.split(',').map((tag) => tag.trim()).filter(Boolean) })} />
          </label>
          <div className="suggested-tags" aria-label={`Suggested tags for ${item.name}`}>
            {item.suggestedTags.map((tag) => (
              <button type="button" key={tag} onClick={() => onAcceptTag(item.id, tag)}>{tag}</button>
            ))}
          </div>
          {item.validationError ? <p className="wardrobe-error">{item.validationError}</p> : null}
          {item.warnings.length > 0 ? (
            <div className="wardrobe-warning" role="status">
              <strong>Needs better photo?</strong>
              <ul>
                {item.warnings.map((warning) => <li key={warning}>{warning}</li>)}
              </ul>
            </div>
          ) : null}
          {item.error ? <p className="wardrobe-error">{item.error}</p> : null}
        </article>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Create `GarmentEditor.tsx`**

Create `outfit_planner_front/src/features/wardrobe/GarmentEditor.tsx`:

```tsx
import { useEffect, useState } from 'react';
import type { UpdateGarmentInput } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';
import { GARMENT_CATEGORIES } from '../outfits/outfitUtils';

export function GarmentEditor({
  garment,
  isSaving,
  onCancel,
  onSave
}: {
  garment: GarmentItem;
  isSaving: boolean;
  onCancel: () => void;
  onSave: (garmentId: string, input: UpdateGarmentInput) => void;
}) {
  const [form, setForm] = useState({
    name: garment.name,
    category: garment.category,
    tags: garment.tags.join(', '),
    primaryColor: garment.primaryColor ?? '',
    season: (garment.season ?? []).join(', ')
  });

  useEffect(() => {
    setForm({
      name: garment.name,
      category: garment.category,
      tags: garment.tags.join(', '),
      primaryColor: garment.primaryColor ?? '',
      season: (garment.season ?? []).join(', ')
    });
  }, [garment]);

  return (
    <form
      className="wardrobe-rail-form"
      aria-label={`Edit ${garment.name}`}
      onSubmit={(event) => {
        event.preventDefault();
        onSave(garment.id, {
          name: form.name,
          category: form.category,
          tags: splitTokens(form.tags),
          primaryColor: form.primaryColor.trim() || null,
          season: splitTokens(form.season)
        });
      }}
    >
      <div className="wardrobe-rail-heading">
        <span>Edit garment</span>
        <h2>{garment.name}</h2>
      </div>
      <label>
        <span>Name</span>
        <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
      </label>
      <label>
        <span>Type</span>
        <select value={form.category} onChange={(event) => setForm({ ...form, category: event.target.value as GarmentCategory })}>
          {GARMENT_CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
        </select>
      </label>
      <label>
        <span>Color</span>
        <input value={form.primaryColor} onChange={(event) => setForm({ ...form, primaryColor: event.target.value })} />
      </label>
      <label>
        <span>Season</span>
        <input value={form.season} onChange={(event) => setForm({ ...form, season: event.target.value })} />
      </label>
      <label>
        <span>Tags</span>
        <input value={form.tags} onChange={(event) => setForm({ ...form, tags: event.target.value })} />
      </label>
      <button type="submit" className="wardrobe-primary-button" disabled={isSaving}>{isSaving ? 'Saving' : 'Save changes'}</button>
      <button type="button" className="wardrobe-secondary-button" onClick={onCancel}>Cancel</button>
    </form>
  );
}

function splitTokens(value: string): string[] {
  return value.split(',').map((token) => token.trim()).filter(Boolean);
}
```

- [ ] **Step 5: Create `WardrobeUploadPanel.tsx`**

Create `outfit_planner_front/src/features/wardrobe/WardrobeUploadPanel.tsx`:

```tsx
import { Camera, CloudUpload, Plus } from 'lucide-react';
import type { DragEvent } from 'react';
import type { GarmentCategory } from '../../types';
import { cleanPhotoChecklist, type UploadQueueItem } from './wardrobeUpload';
import { UploadQueue } from './UploadQueue';

export function WardrobeUploadPanel({
  queue,
  isUploading,
  onAcceptTag,
  onAddFiles,
  onChangeItem,
  onRemoveItem,
  onSubmitAll
}: {
  queue: UploadQueueItem[];
  isUploading: boolean;
  onAcceptTag: (itemId: string, tag: string) => void;
  onAddFiles: (files: File[]) => void;
  onChangeItem: (itemId: string, updates: Partial<Pick<UploadQueueItem, 'name' | 'category' | 'tags' | 'primaryColor' | 'season'>>) => void;
  onRemoveItem: (itemId: string) => void;
  onSubmitAll: () => void;
}) {
  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
    onAddFiles(Array.from(event.dataTransfer.files));
  }

  return (
    <section className="wardrobe-rail" aria-label="Add garment">
      <div className="wardrobe-rail-heading">
        <span>Add garment</span>
        <h2>Catalog clean photos</h2>
      </div>
      <div className="clean-checklist" aria-label="Clean photo checklist">
        {cleanPhotoChecklist.map((item) => <span key={item}>{item}</span>)}
      </div>
      <label className="wardrobe-drop-zone" onDragOver={(event) => event.preventDefault()} onDrop={handleDrop}>
        <CloudUpload size={24} />
        <strong>Upload photos</strong>
        <span>Drag and drop or click to browse. JPG, PNG, WebP, up to 50 MB.</span>
        <input
          aria-label="Garment photos"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          multiple
          onChange={(event) => onAddFiles(Array.from(event.target.files ?? []))}
        />
      </label>
      <label className="wardrobe-camera-input">
        <Camera size={17} />
        <span>Open camera</span>
        <input
          aria-label="Camera garment photo"
          type="file"
          accept="image/*"
          capture="environment"
          onChange={(event) => onAddFiles(Array.from(event.target.files ?? []))}
        />
      </label>
      <UploadQueue items={queue} onAcceptTag={onAcceptTag} onChangeItem={(itemId, updates) => {
        const typedUpdates = updates.category ? { ...updates, category: updates.category as GarmentCategory } : updates;
        onChangeItem(itemId, typedUpdates);
      }} onRemove={onRemoveItem} />
      <button type="button" className="wardrobe-primary-button" disabled={isUploading || queue.every((item) => item.status === 'invalid')} onClick={onSubmitAll}>
        <Plus size={16} />
        {isUploading ? 'Uploading' : 'Add garments'}
      </button>
    </section>
  );
}
```

- [ ] **Step 6: Run TypeScript compile through targeted test command**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/wardrobe/wardrobeFilters.test.ts src/features/wardrobe/wardrobeUpload.test.ts
cd ..
```

Expected: helper tests still pass. If TypeScript reports component compile errors during the test transform, fix the exact import or prop type reported.

- [ ] **Step 7: Commit component surfaces**

Run:

```powershell
git add outfit_planner_front\src\features\wardrobe\GarmentCard.tsx outfit_planner_front\src\features\wardrobe\GarmentEditor.tsx outfit_planner_front\src\features\wardrobe\UploadQueue.tsx outfit_planner_front\src\features\wardrobe\WardrobeFilters.tsx outfit_planner_front\src\features\wardrobe\WardrobeUploadPanel.tsx
git commit -m "Add wardrobe editorial components"
```

## Task 4: Wardrobe Route Integration

**Files:**

- Create: `outfit_planner_front/src/features/wardrobe/wardrobeMutations.ts`
- Modify: `outfit_planner_front/src/routes/WardrobePage.tsx`
- Modify: `outfit_planner_front/src/routes/WardrobePage.test.tsx`

- [ ] **Step 1: Replace WardrobePage tests with full UX coverage**

Replace `outfit_planner_front/src/routes/WardrobePage.test.tsx` with:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WardrobePage } from './WardrobePage';

const garmentsResponse = [
  {
    id: 'garment-1',
    userId: 'user-1',
    name: 'Black silk cami',
    category: 'Top',
    bodyZone: 'Torso',
    imageUrl: '/uploads/black-silk-cami.png',
    thumbnailUrl: '/uploads/black-silk-cami.png',
    tags: ['silk', 'evening'],
    primaryColor: 'black',
    secondaryColors: [],
    material: 'silk',
    brand: null,
    size: null,
    season: ['summer'],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: false,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    createdAt: '2026-06-20T12:00:00Z'
  },
  {
    id: 'garment-2',
    userId: 'user-1',
    name: 'Wool blazer',
    category: 'Outerwear',
    bodyZone: 'OuterLayer',
    imageUrl: '/uploads/wool-blazer.png',
    thumbnailUrl: '/uploads/wool-blazer.png',
    tags: ['work'],
    primaryColor: 'brown',
    secondaryColors: [],
    material: 'wool',
    brand: null,
    size: null,
    season: ['fall'],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: true,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    createdAt: '2026-06-20T12:00:00Z'
  }
];

function renderWardrobe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <WardrobePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('WardrobePage', () => {
  beforeEach(() => {
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:preview'),
      revokeObjectURL: vi.fn()
    });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders editorial search filters checklist and garment cards', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    expect(await screen.findByRole('heading', { name: /every piece has/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/search wardrobe/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/category filter/i)).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /outerwear/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/clean photo checklist/i)).toHaveTextContent(/front view/i);
    expect(screen.getByText(/black silk cami/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/garments?archived=false&sort=recent', expect.any(Object));
  });

  it('calls the garment list endpoint with active filters', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await screen.findByText(/black silk cami/i);
    await userEvent.type(screen.getByLabelText(/search wardrobe/i), 'silk');
    await userEvent.selectOptions(screen.getByLabelText(/category filter/i), 'Top');
    await userEvent.selectOptions(screen.getByLabelText(/^color$/i), 'black');
    await userEvent.selectOptions(screen.getByLabelText(/^season$/i), 'summer');
    await userEvent.click(screen.getByLabelText(/favorites/i));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments?q=silk&category=Top&color=black&season=summer&favorite=true&archived=false&sort=recent', expect.any(Object));
    });
  });

  it('shows empty examples and reset for filtered empty states', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse([]));

    renderWardrobe();

    expect(await screen.findByText(/start with a front-view shirt/i)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText(/search wardrobe/i), 'does not exist');

    expect(await screen.findByRole('button', { name: /reset filters/i })).toBeInTheDocument();
  });

  it('favorites archives edits duplicates and deletes garments through existing API calls', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await userEvent.click(await screen.findByRole('button', { name: /favorite black silk cami/i }));
    await userEvent.click(await screen.findByRole('button', { name: /archive black silk cami/i }));
    await userEvent.click(await screen.findByRole('button', { name: /duplicate black silk cami/i }));
    await userEvent.click(await screen.findByRole('button', { name: /edit black silk cami/i }));
    await userEvent.clear(await screen.findByLabelText(/^name$/i));
    await userEvent.type(screen.getByLabelText(/^name$/i), 'Black silk camisole');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    await userEvent.click(await screen.findByRole('button', { name: /delete black silk cami/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('"isFavorite":true') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('"isArchived":true') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Black silk cami copy') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('Black silk camisole') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('supports bulk upload file input camera input drag drop suggestions warnings and submit all', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await screen.findByText(/black silk cami/i);
    const fileInput = screen.getByLabelText(/garment photos/i);
    const cameraInput = screen.getByLabelText(/camera garment photo/i);
    expect(cameraInput).toHaveAttribute('capture', 'environment');

    const shirt = new File(['shirt'], 'cream-linen-shirt.png', { type: 'image/png' });
    const tiny = new File(['x'], 'IMG_0001.png', { type: 'image/png' });
    await userEvent.upload(fileInput, [shirt, tiny]);

    expect(await screen.findByLabelText(/upload queue/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue(/cream linen shirt/i)).toBeInTheDocument();
    expect(screen.getByText(/needs better photo/i)).toBeInTheDocument();
    expect(within(screen.getByLabelText(/suggested tags for cream linen shirt/i)).getByRole('button', { name: /linen/i })).toBeInTheDocument();

    const dropZone = screen.getByText(/upload photos/i).closest('label');
    expect(dropZone).not.toBeNull();
    fireEvent.drop(dropZone!, {
      dataTransfer: {
        files: [new File(['coat'], 'brown-wool-blazer.webp', { type: 'image/webp' })]
      }
    });
    expect(await screen.findByDisplayValue(/brown wool blazer/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /add garments/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/uploads/garment', expect.objectContaining({ method: 'POST' }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Cream linen shirt') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Brown wool blazer') }));
    });
  });
});

function mockWardrobeFetch() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input);

    if (url.startsWith('/api/garments') && (!init || init.method === undefined)) {
      return jsonResponse(garmentsResponse);
    }

    if (url.endsWith('/uploads/garment') && init?.method === 'POST') {
      return jsonResponse({ url: '/uploads/new-garment.png' }, 201);
    }

    if (url.endsWith('/garments') && init?.method === 'POST') {
      return jsonResponse({ ...garmentsResponse[0], id: `created-${Date.now()}` }, 201);
    }

    if (url.includes('/garments/garment-1') && init?.method === 'PATCH') {
      return jsonResponse({ ...garmentsResponse[0], name: 'Black silk camisole' });
    }

    if (url.includes('/garments/garment-1') && init?.method === 'DELETE') {
      return new Response(null, { status: 204 });
    }

    return jsonResponse([]);
  });
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

- [ ] **Step 2: Run Wardrobe tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/WardrobePage.test.tsx
cd ..
```

Expected: fails because the new labels, actions, filters, queue, and editorial heading are not implemented.

- [ ] **Step 3: Create mutation hook**

Create `outfit_planner_front/src/features/wardrobe/wardrobeMutations.ts`:

```ts
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createGarment, deleteGarment, updateGarment, uploadGarmentPhoto, type UpdateGarmentInput } from '../../api/client';
import type { GarmentItem } from '../../types';
import { duplicateGarmentInput } from './wardrobeFilters';
import type { UploadQueueItem } from './wardrobeUpload';

export const wardrobeQueryKey = ['garments'] as const;

export function useWardrobeMutations() {
  const queryClient = useQueryClient();
  const invalidateWardrobe = () => {
    void queryClient.invalidateQueries({ queryKey: wardrobeQueryKey });
  };

  const favoriteMutation = useMutation({
    mutationFn: (garment: GarmentItem) => updateGarment(garment.id, { isFavorite: !garment.isFavorite }),
    onSuccess: invalidateWardrobe
  });

  const archiveMutation = useMutation({
    mutationFn: (garment: GarmentItem) => updateGarment(garment.id, { isArchived: !garment.isArchived }),
    onSuccess: invalidateWardrobe
  });

  const editMutation = useMutation({
    mutationFn: ({ garmentId, input }: { garmentId: string; input: UpdateGarmentInput }) => updateGarment(garmentId, input),
    onSuccess: invalidateWardrobe
  });

  const duplicateMutation = useMutation({
    mutationFn: (garment: GarmentItem) => createGarment(duplicateGarmentInput(garment)),
    onSuccess: invalidateWardrobe
  });

  const deleteMutation = useMutation({
    mutationFn: deleteGarment,
    onSuccess: invalidateWardrobe
  });

  const uploadQueueMutation = useMutation({
    mutationFn: async (items: UploadQueueItem[]) => {
      const validItems = items.filter((item) => !item.validationError);
      const created: GarmentItem[] = [];

      for (const item of validItems) {
        const uploadedPhoto = await uploadGarmentPhoto(item.file);
        created.push(await createGarment({
          name: item.name,
          category: item.category,
          imageUrl: uploadedPhoto.url,
          thumbnailUrl: uploadedPhoto.url,
          tags: item.tags,
          primaryColor: item.primaryColor.trim() || null,
          season: item.season
        }));
      }

      return created;
    },
    onSuccess: invalidateWardrobe
  });

  return {
    favoriteMutation,
    archiveMutation,
    editMutation,
    duplicateMutation,
    deleteMutation,
    uploadQueueMutation
  };
}
```

- [ ] **Step 4: Replace WardrobePage with route orchestrator**

Replace `outfit_planner_front/src/routes/WardrobePage.tsx` with:

```tsx
import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { GarmentItem } from '../types';
import { listGarments } from '../api/client';
import { GarmentCard } from '../features/wardrobe/GarmentCard';
import { GarmentEditor } from '../features/wardrobe/GarmentEditor';
import { WardrobeFilters } from '../features/wardrobe/WardrobeFilters';
import { WardrobeUploadPanel } from '../features/wardrobe/WardrobeUploadPanel';
import { defaultWardrobeFilters, filterGarmentsByLocalTags, toGarmentFilters, type WardrobeFilterState } from '../features/wardrobe/wardrobeFilters';
import { useWardrobeMutations, wardrobeQueryKey } from '../features/wardrobe/wardrobeMutations';
import { createUploadQueueItems, updateUploadQueueItem, type UploadQueueItem } from '../features/wardrobe/wardrobeUpload';
import '../features/wardrobe/wardrobe.css';

export function WardrobePage() {
  const [filters, setFilters] = useState<WardrobeFilterState>(defaultWardrobeFilters);
  const [editingGarment, setEditingGarment] = useState<GarmentItem | null>(null);
  const [uploadQueue, setUploadQueue] = useState<UploadQueueItem[]>([]);
  const apiFilters = useMemo(() => toGarmentFilters(filters), [filters]);
  const garmentsQuery = useQuery({
    queryKey: [...wardrobeQueryKey, apiFilters],
    queryFn: () => listGarments(apiFilters)
  });
  const mutations = useWardrobeMutations();
  const garments = filterGarmentsByLocalTags(garmentsQuery.data ?? [], filters.tag);

  function addFiles(files: File[]) {
    setEditingGarment(null);
    setUploadQueue((current) => [
      ...current,
      ...createUploadQueueItems(files, {
        category: filters.category === 'All' ? 'Top' : filters.category,
        color: filters.color,
        season: filters.season ? [filters.season] : [],
        existingTags: Array.from(new Set((garmentsQuery.data ?? []).flatMap((garment) => garment.tags))).slice(0, 6)
      })
    ]);
  }

  function changeQueueItem(itemId: string, updates: Partial<Pick<UploadQueueItem, 'name' | 'category' | 'tags' | 'primaryColor' | 'season'>>) {
    setUploadQueue((current) => current.map((item) => item.id === itemId ? updateUploadQueueItem(item, updates) : item));
  }

  function acceptSuggestedTag(itemId: string, tag: string) {
    setUploadQueue((current) => current.map((item) => {
      if (item.id !== itemId || item.tags.includes(tag)) {
        return item;
      }

      return updateUploadQueueItem(item, { tags: [...item.tags, tag] });
    }));
  }

  function resetFilters() {
    setFilters(defaultWardrobeFilters);
  }

  const hasActiveFilters = JSON.stringify(filters) !== JSON.stringify(defaultWardrobeFilters);

  return (
    <section className="wardrobe-editorial-page">
      <div className="wardrobe-main">
        <header className="wardrobe-hero">
          <span>My wardrobe</span>
          <h1>Every piece has <em>a purpose.</em></h1>
        </header>
        <WardrobeFilters filters={filters} itemCount={garments.length} onChange={setFilters} onReset={resetFilters} />
        {garmentsQuery.isLoading ? (
          <div className="wardrobe-skeleton-grid" aria-label="Loading wardrobe">
            {Array.from({ length: 8 }).map((_, index) => <span key={index} />)}
          </div>
        ) : garments.length === 0 ? (
          <WardrobeEmptyState filtered={hasActiveFilters} onReset={resetFilters} />
        ) : (
          <div className="wardrobe-catalog" aria-label="Wardrobe catalog">
            {garments.map((garment) => (
              <GarmentCard
                key={garment.id}
                garment={garment}
                pendingAction={pendingActionFor(garment, mutations)}
                onArchive={(item) => mutations.archiveMutation.mutate(item)}
                onDelete={(item) => mutations.deleteMutation.mutate(item.id)}
                onDuplicate={(item) => mutations.duplicateMutation.mutate(item)}
                onEdit={setEditingGarment}
                onFavorite={(item) => mutations.favoriteMutation.mutate(item)}
              />
            ))}
          </div>
        )}
        {[garmentsQuery.error, mutations.favoriteMutation.error, mutations.archiveMutation.error, mutations.editMutation.error, mutations.duplicateMutation.error, mutations.deleteMutation.error, mutations.uploadQueueMutation.error]
          .filter(Boolean)
          .map((error) => <p className="wardrobe-error" key={(error as Error).message}>{(error as Error).message}</p>)}
      </div>
      {editingGarment ? (
        <GarmentEditor
          garment={editingGarment}
          isSaving={mutations.editMutation.isPending}
          onCancel={() => setEditingGarment(null)}
          onSave={(garmentId, input) => mutations.editMutation.mutate({ garmentId, input }, { onSuccess: () => setEditingGarment(null) })}
        />
      ) : (
        <WardrobeUploadPanel
          queue={uploadQueue}
          isUploading={mutations.uploadQueueMutation.isPending}
          onAcceptTag={acceptSuggestedTag}
          onAddFiles={addFiles}
          onChangeItem={changeQueueItem}
          onRemoveItem={(itemId) => setUploadQueue((current) => current.filter((item) => item.id !== itemId))}
          onSubmitAll={() => mutations.uploadQueueMutation.mutate(uploadQueue, { onSuccess: () => setUploadQueue([]) })}
        />
      )}
    </section>
  );
}

function WardrobeEmptyState({ filtered, onReset }: { filtered: boolean; onReset: () => void }) {
  return (
    <section className="wardrobe-empty">
      <h2>{filtered ? 'No pieces match these filters' : 'Start with a front-view shirt, jeans, shoes, and one outer layer.'}</h2>
      <p>{filtered ? 'Reset filters to return to the full closet.' : 'A few clean photos are enough to make Builder and Calendar useful.'}</p>
      {filtered ? <button type="button" className="wardrobe-secondary-button" onClick={onReset}>Reset filters</button> : null}
    </section>
  );
}

function pendingActionFor(garment: GarmentItem, mutations: ReturnType<typeof useWardrobeMutations>): string | undefined {
  if (mutations.favoriteMutation.isPending && mutations.favoriteMutation.variables?.id === garment.id) {
    return 'favorite';
  }
  if (mutations.archiveMutation.isPending && mutations.archiveMutation.variables?.id === garment.id) {
    return 'archive';
  }
  if (mutations.duplicateMutation.isPending && mutations.duplicateMutation.variables?.id === garment.id) {
    return 'duplicate';
  }
  if (mutations.deleteMutation.isPending && mutations.deleteMutation.variables === garment.id) {
    return 'delete';
  }
  return undefined;
}
```

- [ ] **Step 5: Run Wardrobe tests and fix RED mismatches**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/WardrobePage.test.tsx
cd ..
```

Expected: the test may fail on exact accessible names, generated query string order, or TypeScript property names. Fix only the test or implementation mismatch shown in the output, keeping the behaviors from Step 1 intact.

- [ ] **Step 6: Run helper and Wardrobe tests together**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/wardrobe/wardrobeFilters.test.ts src/features/wardrobe/wardrobeUpload.test.ts src/routes/WardrobePage.test.tsx
cd ..
```

Expected: helper and Wardrobe route tests pass.

- [ ] **Step 7: Commit route integration**

Run:

```powershell
git add outfit_planner_front\src\features\wardrobe\wardrobeMutations.ts outfit_planner_front\src\routes\WardrobePage.tsx outfit_planner_front\src\routes\WardrobePage.test.tsx
git commit -m "Implement wardrobe UX workflows"
```

## Task 5: Editorial Wardrobe Styling

**Files:**

- Create: `outfit_planner_front/src/features/wardrobe/wardrobe.css`

- [ ] **Step 1: Add wardrobe editorial CSS**

Create `outfit_planner_front/src/features/wardrobe/wardrobe.css`:

```css
.wardrobe-editorial-page {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  min-height: 100vh;
}

.wardrobe-main {
  border-right: 1px solid var(--editorial-line);
  min-width: 0;
  padding: 1.65rem 1.65rem 2rem;
}

.wardrobe-hero {
  align-items: end;
  display: flex;
  gap: 1.25rem;
  justify-content: space-between;
  margin-bottom: 1.35rem;
}

.wardrobe-hero span,
.wardrobe-rail-heading span {
  color: var(--editorial-muted);
  display: block;
  font-size: 0.66rem;
  font-weight: 900;
  letter-spacing: 0;
  margin-bottom: 0.25rem;
  text-transform: uppercase;
}

.wardrobe-hero h1,
.wardrobe-rail-heading h2,
.wardrobe-empty h2 {
  color: var(--editorial-ink);
  font-family: var(--editorial-font-display);
  font-size: 3.4rem;
  font-weight: 400;
  letter-spacing: 0;
  line-height: 0.95;
  margin: 0;
}

.wardrobe-hero em {
  color: var(--editorial-crimson);
  font-style: italic;
}

.wardrobe-controls {
  display: grid;
  gap: 0.8rem;
  margin-bottom: 1rem;
}

.wardrobe-search-row,
.wardrobe-tab-row,
.wardrobe-filter-row {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
}

.wardrobe-search {
  align-items: center;
  background: var(--editorial-panel);
  border: 1px solid var(--editorial-line);
  border-radius: 10px;
  display: flex;
  gap: 0.55rem;
  min-width: min(320px, 100%);
  padding: 0 0.8rem;
}

.wardrobe-controls input,
.wardrobe-controls select,
.wardrobe-rail input,
.wardrobe-rail select,
.wardrobe-rail-form input,
.wardrobe-rail-form select {
  background: var(--editorial-panel);
  border: 1px solid var(--editorial-line);
  border-radius: 8px;
  color: var(--editorial-ink);
  min-height: 2.5rem;
  padding: 0 0.75rem;
}

.wardrobe-search input {
  border: 0;
  flex: 1;
  min-width: 0;
  padding: 0;
}

.wardrobe-tab-row {
  border-bottom: 1px solid var(--editorial-line);
}

.wardrobe-tab-row button {
  background: transparent;
  border: 0;
  border-radius: 0;
  color: var(--editorial-muted);
  font-size: 0.85rem;
  font-weight: 800;
  min-height: 2.4rem;
  padding: 0 0.25rem;
}

.wardrobe-tab-row button[aria-selected="true"] {
  box-shadow: inset 0 -2px 0 var(--editorial-crimson);
  color: var(--editorial-ink);
}

.wardrobe-item-count {
  color: var(--editorial-muted);
  font-size: 0.82rem;
  font-weight: 800;
  margin-left: auto;
}

.wardrobe-filter-row {
  background: color-mix(in srgb, var(--editorial-panel) 72%, transparent);
  border: 1px solid var(--editorial-line);
  border-radius: 12px;
  padding: 0.55rem;
}

.wardrobe-filter-row label,
.wardrobe-rail label,
.wardrobe-rail-form label {
  color: var(--editorial-muted);
  display: grid;
  font-size: 0.76rem;
  font-weight: 900;
  gap: 0.35rem;
}

.wardrobe-check {
  align-items: center;
  display: inline-flex;
  grid-auto-flow: column;
}

.wardrobe-catalog,
.wardrobe-skeleton-grid {
  display: grid;
  gap: 0.8rem;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
}

.wardrobe-card {
  background: var(--editorial-panel);
  border: 1px solid var(--editorial-line);
  border-radius: 8px;
  overflow: hidden;
}

.wardrobe-card.archived {
  opacity: 0.58;
}

.wardrobe-card-image {
  aspect-ratio: 4 / 3;
  background: var(--editorial-panel-soft);
  position: relative;
}

.wardrobe-card-image img {
  height: 100%;
  object-fit: contain;
  padding: 0.75rem;
  width: 100%;
}

.wardrobe-icon-button {
  background: transparent;
  color: var(--editorial-muted);
  min-height: 2rem;
  padding: 0.35rem;
  position: absolute;
  top: 0.45rem;
  left: 0.45rem;
}

.wardrobe-icon-button.active {
  color: var(--editorial-crimson);
}

.wardrobe-card-body {
  display: grid;
  gap: 0.5rem;
  padding: 0.75rem;
}

.wardrobe-card h3 {
  font-size: 0.9rem;
  margin: 0;
}

.wardrobe-card p {
  color: var(--editorial-muted);
  font-size: 0.76rem;
  font-weight: 700;
  margin: 0.15rem 0 0;
}

.wardrobe-card-actions {
  align-items: center;
  display: flex;
  gap: 0.25rem;
  justify-content: flex-end;
}

.wardrobe-card-actions button,
.wardrobe-view-buttons button,
.wardrobe-ghost-button,
.wardrobe-secondary-button {
  background: transparent;
  border: 1px solid var(--editorial-line);
  border-radius: 8px;
  color: var(--editorial-ink);
  min-height: 2.15rem;
  padding: 0 0.65rem;
}

.wardrobe-rail,
.wardrobe-rail-form {
  background: var(--editorial-panel);
  display: grid;
  gap: 1rem;
  min-width: 0;
  padding: 1.35rem;
}

.wardrobe-rail-heading h2 {
  font-size: 1.7rem;
}

.clean-checklist {
  display: grid;
  gap: 0.45rem;
}

.clean-checklist span {
  border: 1px solid var(--editorial-line);
  border-radius: 999px;
  color: var(--editorial-muted);
  font-size: 0.8rem;
  font-weight: 800;
  padding: 0.45rem 0.7rem;
}

.wardrobe-drop-zone {
  align-items: center;
  border: 1px dashed var(--editorial-line-strong);
  border-radius: 12px;
  display: grid;
  gap: 0.35rem;
  justify-items: center;
  min-height: 9rem;
  padding: 1rem;
  text-align: center;
}

.wardrobe-drop-zone input,
.wardrobe-camera-input input {
  height: 1px;
  opacity: 0;
  position: absolute;
  width: 1px;
}

.wardrobe-camera-input {
  align-items: center;
  border: 1px solid var(--editorial-line);
  border-radius: 8px;
  display: flex;
  font-weight: 800;
  gap: 0.55rem;
  min-height: 2.6rem;
  padding: 0 0.75rem;
  position: relative;
}

.upload-queue {
  display: grid;
  gap: 0.75rem;
}

.upload-queue-row {
  border: 1px solid var(--editorial-line);
  border-radius: 12px;
  display: grid;
  gap: 0.6rem;
  padding: 0.75rem;
}

.upload-queue-row.invalid {
  border-color: color-mix(in srgb, var(--editorial-crimson) 40%, var(--editorial-line));
}

.upload-queue-heading {
  align-items: center;
  display: flex;
  gap: 0.75rem;
  justify-content: space-between;
}

.suggested-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.suggested-tags button {
  background: var(--editorial-panel-soft);
  border: 1px solid var(--editorial-line);
  border-radius: 999px;
  color: var(--editorial-muted);
  min-height: 1.9rem;
  padding: 0 0.55rem;
}

.wardrobe-primary-button {
  background: linear-gradient(180deg, #e0143d, var(--editorial-crimson-deep));
  border: 1px solid color-mix(in srgb, var(--editorial-crimson) 80%, black);
  border-radius: 8px;
  box-shadow: 0 8px 0 #8c041e, 0 18px 30px rgba(213, 9, 47, 0.22);
  color: #fff8ee;
  font-weight: 900;
  min-height: 2.85rem;
}

.wardrobe-primary-button:active {
  box-shadow: 0 3px 0 #8c041e, 0 10px 20px rgba(213, 9, 47, 0.2);
  transform: translateY(4px);
}

.wardrobe-error,
.wardrobe-warning {
  border-radius: 10px;
  font-size: 0.82rem;
  margin: 0;
  padding: 0.75rem;
}

.wardrobe-error {
  background: color-mix(in srgb, var(--editorial-crimson) 10%, transparent);
  border: 1px solid color-mix(in srgb, var(--editorial-crimson) 30%, transparent);
  color: var(--editorial-crimson);
}

.wardrobe-warning {
  background: color-mix(in srgb, #c47a13 12%, transparent);
  border: 1px solid color-mix(in srgb, #c47a13 30%, transparent);
  color: var(--editorial-ink);
}

.wardrobe-warning ul {
  margin: 0.45rem 0 0;
  padding-left: 1rem;
}

.wardrobe-empty {
  border: 1px solid var(--editorial-line);
  border-radius: 12px;
  padding: 1.4rem;
}

.wardrobe-empty h2 {
  font-size: 2rem;
}

.wardrobe-skeleton-grid span {
  background: var(--editorial-panel);
  border: 1px solid var(--editorial-line);
  border-radius: 8px;
  min-height: 210px;
}

.sr-only {
  clip: rect(0, 0, 0, 0);
  border: 0;
  height: 1px;
  margin: -1px;
  overflow: hidden;
  padding: 0;
  position: absolute;
  white-space: nowrap;
  width: 1px;
}

@media (max-width: 1120px) {
  .wardrobe-editorial-page {
    grid-template-columns: 1fr;
  }

  .wardrobe-main {
    border-right: 0;
  }

  .wardrobe-rail,
  .wardrobe-rail-form {
    border-top: 1px solid var(--editorial-line);
  }
}

@media (max-width: 680px) {
  .wardrobe-main {
    padding: 1rem;
  }

  .wardrobe-hero {
    align-items: flex-start;
    display: grid;
  }

  .wardrobe-hero h1 {
    font-size: 2.6rem;
  }

  .wardrobe-catalog {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .wardrobe-filter-row label,
  .wardrobe-filter-row input,
  .wardrobe-filter-row select,
  .wardrobe-filter-row button {
    width: 100%;
  }
}
```

- [ ] **Step 2: Run Wardrobe tests after CSS import**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/WardrobePage.test.tsx
cd ..
```

Expected: tests pass. CSS itself is not asserted heavily; this command catches import and transform problems.

- [ ] **Step 3: Commit CSS**

Run:

```powershell
git add outfit_planner_front\src\features\wardrobe\wardrobe.css
git commit -m "Style wardrobe editorial surface"
```

## Task 6: Documentation

**Files:**

- Modify: `README.md`
- Modify: `agents.md` only if implementation changed durable context beyond the existing Obra/Crimson note.

- [ ] **Step 1: Update README feature notes**

In `README.md`, add a short feature bullet near the existing frontend or feature list:

```markdown
- Wardrobe uses the editorial Obra/Crimson-inspired interface for catalog search, filters, edit, duplicate, archive, favorite, bulk upload, drag-and-drop upload, mobile camera capture, clean photo guidance, local tag suggestions, and photo quality warnings.
```

- [ ] **Step 2: Run docs diff**

Run:

```powershell
git diff -- README.md agents.md
```

Expected: diff contains only the Wardrobe UX note and any genuinely necessary durable context update.

- [ ] **Step 3: Commit docs**

Run:

```powershell
git add README.md agents.md
git commit -m "Document wardrobe UX update"
```

If `agents.md` did not change in this task, run:

```powershell
git add README.md
git commit -m "Document wardrobe UX update"
```

## Task 7: Full Verification And Visual Check

**Files:**

- No planned source changes. Fix only defects exposed by the commands below.

- [ ] **Step 1: Run targeted Wardrobe tests**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/wardrobe/wardrobeFilters.test.ts src/features/wardrobe/wardrobeUpload.test.ts src/routes/WardrobePage.test.tsx src/app/AppShell.test.tsx
cd ..
```

Expected: all targeted tests pass.

- [ ] **Step 2: Run full frontend test suite**

Run:

```powershell
cd outfit_planner_front
npm test
cd ..
```

Expected: all Vitest tests pass.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
cd outfit_planner_front
npm run build
cd ..
```

Expected: `tsc -b` and Vite build pass. Generated OpenAPI artifacts remain ignored.

- [ ] **Step 4: Run Playwright e2e smoke**

Run:

```powershell
cd outfit_planner_front
npm run test:e2e
cd ..
```

Expected: existing Playwright register-upload-plan-share smoke passes. If it fails because accessible names changed, update only selectors to the new user-facing labels and rerun.

- [ ] **Step 5: Start local frontend for visual verification**

Start the API if no backend is already running:

```powershell
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

In a second terminal, start Vite:

```powershell
cd outfit_planner_front
npm run dev
```

Expected: Vite prints a localhost URL, normally `https://localhost:5173/`.

- [ ] **Step 6: Use Browser plugin for desktop and mobile visual checks**

Open the app with the Browser plugin at the Vite URL. Verify these states manually:

- `/wardrobe` light theme matches the user's warm paper reference instead of claymorphism.
- Theme toggle dark mode maps to warm ink, cream text, and crimson accent.
- Desktop layout has left navigation, central catalog, and right rail.
- Mobile viewport stacks Wardrobe content without overlapping text or inaccessible controls.
- Drag-and-drop target, camera input, filters, edit rail, and card actions are visible and usable.

Expected: no obvious layout overlap, no old lavender clay blobs, no convex purple buttons on the Wardrobe surface.

- [ ] **Step 7: Check git status**

Run:

```powershell
git status --short
```

Expected: only intentional source/doc changes are present before the final commit. Generated OpenAPI files, `.superpowers/`, screenshots, upload storage, and `design_references/` remain untracked or ignored and are not staged.

- [ ] **Step 8: Commit verification fixes**

If any fixes were required after full verification, commit them:

```powershell
git add outfit_planner_front\src README.md agents.md
git commit -m "Fix wardrobe UX verification issues"
```

If no fixes were required, do not create an empty commit.

## Final Completion Checklist

- [ ] Wardrobe uses the new editorial visual language, not the old claymorphism palette, blobs, panels, or button style.
- [ ] Left navigation, central catalog, and right rail are present on desktop.
- [ ] Wardrobe remains usable on mobile with bottom navigation.
- [ ] Search and filters work through `listGarments(filters)` plus local tag filtering.
- [ ] Archived garments are hidden by default and recoverable with the archived filter.
- [ ] Favorite, archive, edit, duplicate, and delete use existing API wrapper functions.
- [ ] Bulk upload and drag-and-drop create editable upload queue rows.
- [ ] Mobile camera capture input exists.
- [ ] Clean photo checklist is visible before upload submit.
- [ ] Auto-tag suggestions and advisory photo warnings appear.
- [ ] Existing generated API workflow remains unchanged.
- [ ] Targeted tests, full `npm test`, `npm run build`, and Playwright e2e have run and passed, or exact blockers are documented.
