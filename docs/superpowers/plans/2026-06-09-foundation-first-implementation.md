# Foundation First Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the frontend foundation slice: guarded private routes, split app structure, generated OpenAPI-backed frontend types, PWA basics, stale Builder active-outfit fix, and regression tests.

**Architecture:** Keep existing user-visible flows intact while moving route pages and shared UI out of the current `src/App.tsx` monolith. Use backend OpenAPI output plus ignored generated TypeScript artifacts, with the existing API wrapper staying as the app-facing fetch layer for cookies, CSRF, uploads, and diagnostics.

**Tech Stack:** ASP.NET Core Minimal API on .NET 10, built-in ASP.NET Core OpenAPI, React, TypeScript, Vite, React Router, TanStack Query, Vitest, Testing Library, openapi-typescript, Playwright e2e smoke when browser installation succeeds.

---

## References

- Spec: `docs/superpowers/specs/2026-06-09-foundation-first-design.md`
- Microsoft OpenAPI generation docs: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
- Microsoft OpenAPI overview and build-time generation package notes: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0

## File Map

Create:

- `outfit_planner_front/scripts/generate-api-client.mjs`: cross-platform script that builds backend OpenAPI into an ignored folder and runs `openapi-typescript`.
- `outfit_planner_front/src/api/generated/responseTypes.ts`: committed type aliases derived from ignored generated `schema.ts`.
- `outfit_planner_front/src/api/generated/responseTypes.test.ts`: smoke tests for generated type aliases and scripts.
- `outfit_planner_front/src/app/App.tsx`: top-level route tree.
- `outfit_planner_front/src/app/AppShell.tsx`: clay shell, navigation, theme, auth sidebar, mobile bottom navigation.
- `outfit_planner_front/src/app/RequireAuth.tsx`: route guard.
- `outfit_planner_front/src/app/RequireAuth.test.tsx`: guard behavior tests.
- `outfit_planner_front/src/app/registerServiceWorker.ts`: guarded service worker registration.
- `outfit_planner_front/src/app/pwa.test.ts`: manifest/service-worker smoke tests.
- `outfit_planner_front/src/routes/AuthPage.tsx`
- `outfit_planner_front/src/routes/AuthPage.test.tsx`
- `outfit_planner_front/src/routes/WardrobePage.tsx`
- `outfit_planner_front/src/routes/WardrobePage.test.tsx`
- `outfit_planner_front/src/routes/BuilderPage.tsx`
- `outfit_planner_front/src/routes/BuilderPage.test.tsx`
- `outfit_planner_front/src/routes/CalendarPage.tsx`
- `outfit_planner_front/src/routes/CalendarPage.test.tsx`
- `outfit_planner_front/src/routes/SharePage.tsx`
- `outfit_planner_front/src/routes/SharePage.test.tsx`
- `outfit_planner_front/src/features/auth/AuthActions.tsx`
- `outfit_planner_front/src/features/auth/authQueries.ts`
- `outfit_planner_front/src/features/auth/returnUrl.ts`
- `outfit_planner_front/src/features/auth/returnUrl.test.ts`
- `outfit_planner_front/src/features/builder/BodyReferenceManager.tsx`
- `outfit_planner_front/src/features/builder/OutfitList.tsx`
- `outfit_planner_front/src/features/builder/SlotPicker.tsx`
- `outfit_planner_front/src/features/builder/garmentName.ts`
- `outfit_planner_front/src/features/builder/garmentName.test.ts`
- `outfit_planner_front/src/features/calendar/OutfitChoiceList.tsx`
- `outfit_planner_front/src/features/wardrobe/GarmentColumn.tsx`
- `outfit_planner_front/src/shared/ui/ClayBlobs.tsx`
- `outfit_planner_front/src/shared/ui/ClayDatePicker.tsx`
- `outfit_planner_front/src/shared/ui/EmptyPreview.tsx`
- `outfit_planner_front/src/shared/ui/EmptyState.tsx`
- `outfit_planner_front/src/shared/ui/FilePicker.tsx`
- `outfit_planner_front/src/shared/ui/GarmentCategoryControl.tsx`
- `outfit_planner_front/src/shared/ui/MetricOrb.tsx`
- `outfit_planner_front/src/shared/ui/PageHeader.tsx`
- `outfit_planner_front/src/shared/ui/PanelTitle.tsx`
- `outfit_planner_front/src/shared/ui/Skeletons.tsx`
- `outfit_planner_front/public/manifest.webmanifest`
- `outfit_planner_front/public/offline.html`
- `outfit_planner_front/public/sw.js`
- `outfit_planner_front/public/icons/outfit-icon.svg`
- `outfit_planner_front/e2e/register-upload-plan-share.spec.ts` if Playwright is added in this slice.
- `outfit_planner_front/playwright.config.ts` if Playwright is added in this slice.

Modify:

- `outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj`: add OpenAPI packages.
- `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`: register/map OpenAPI in dev/test and exempt it from auth.
- `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`: add OpenAPI configuration test.
- `.gitignore`: ignore generated OpenAPI and generated TS artifacts.
- `outfit_planner_front/package.json`: add generation scripts, `openapi-typescript`, and Playwright scripts/dependency if browser installation succeeds.
- `outfit_planner_front/package-lock.json`: update through npm install.
- `outfit_planner_front/index.html`: link manifest and theme color.
- `outfit_planner_front/src/main.tsx`: import `src/app/App` and register service worker.
- `outfit_planner_front/src/App.tsx`: reduce to compatibility re-export from `src/app/App`.
- `outfit_planner_front/src/api/client.ts`: import/re-export generated response aliases where practical, preserving request behavior.
- `outfit_planner_front/src/types.ts`: shrink to frontend-only types and re-export API shapes from generated response aliases.
- `outfit_planner_front/src/App.test.tsx`: replace with small compatibility/shell route test or delete after route tests cover behavior.
- `README.md`: update layout, OpenAPI generation, PWA note, and stale category boundary.
- `agents.md`: update durable frontend architecture context if needed.

Generated but ignored:

- `outfit_planner_front/.generated/openapi/*.json`
- `outfit_planner_front/src/api/generated/schema.ts`

## Task 1: Backend OpenAPI Output

**Files:**

- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj`
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`

- [ ] **Step 1: Add failing backend test**

Add a test name to the `tests` list near the other API contract tests:

```csharp
("api exposes openapi document generation", TestApiExposesOpenApiDocumentGeneration),
```

Add this test function near the other API string/config tests:

```csharp
static void TestApiExposesOpenApiDocumentGeneration()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var apiProject = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "OutfitPlanner.Api.csproj"));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(apiProject.Contains("Microsoft.AspNetCore.OpenApi", StringComparison.Ordinal), "api project should reference ASP.NET Core OpenAPI runtime package.");
    AssertTrue(apiProject.Contains("Microsoft.Extensions.ApiDescription.Server", StringComparison.Ordinal), "api project should reference build-time OpenAPI generation package.");
    AssertTrue(program.Contains("builder.Services.AddOpenApi()", StringComparison.Ordinal), "api startup should register OpenAPI services.");
    AssertTrue(program.Contains("app.MapOpenApi(\"/api/openapi/{documentName}.json\")", StringComparison.Ordinal), "api startup should map OpenAPI JSON under /api.");
    AssertTrue(program.Contains("path.StartsWith(\"/openapi/\", StringComparison.OrdinalIgnoreCase)", StringComparison.Ordinal), "OpenAPI endpoint should not require an auth session.");
}
```

- [ ] **Step 2: Run backend test and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails only `api exposes openapi document generation` because the packages and startup calls are missing.

- [ ] **Step 3: Add OpenAPI packages**

Edit `outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj` package references:

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.2">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Keep the existing auth and DbUp package references unchanged.

- [ ] **Step 4: Register and map OpenAPI**

In `Program.cs`, after JSON options are configured and before app build, add:

```csharp
builder.Services.AddOpenApi();
```

After `var api = app.MapGroup("/api");`, add:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    app.MapOpenApi("/api/openapi/{documentName}.json");
}
```

In `RequiresAuthenticatedUser`, add the OpenAPI exemption:

```csharp
&& !path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase)
```

The return block should include the new exemption alongside health, system status, auth, storage, and public share exemptions.

- [ ] **Step 5: Run backend tests and build**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: all backend console tests pass and API builds.

- [ ] **Step 6: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj outfit_planner_back\src\OutfitPlanner.Api\Program.cs outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs
git commit -m "Add backend OpenAPI generation"
```

## Task 2: Frontend Generated API Type Workflow

**Files:**

- Create: `outfit_planner_front/scripts/generate-api-client.mjs`
- Create: `outfit_planner_front/src/api/generated/responseTypes.ts`
- Create: `outfit_planner_front/src/api/generated/responseTypes.test.ts`
- Modify: `.gitignore`
- Modify: `outfit_planner_front/package.json`
- Modify: `outfit_planner_front/package-lock.json`
- Modify: `outfit_planner_front/src/types.ts`
- Modify: `outfit_planner_front/src/api/client.ts`

- [ ] **Step 1: Install generator dependency**

Run:

```powershell
cd outfit_planner_front
npm install --save-dev openapi-typescript@7.13.0
cd ..
```

Expected: `package.json` and `package-lock.json` update.

- [ ] **Step 2: Add failing generator workflow test**

Create `outfit_planner_front/src/api/generated/responseTypes.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

const frontendRoot = path.resolve(__dirname, '../../..');
const repoRoot = path.resolve(frontendRoot, '..');

describe('generated API workflow', () => {
  it('keeps generated OpenAPI and schema artifacts ignored', () => {
    const gitignore = fs.readFileSync(path.join(repoRoot, '.gitignore'), 'utf8');

    expect(gitignore).toContain('outfit_planner_front/.generated/');
    expect(gitignore).toContain('outfit_planner_front/src/api/generated/schema.ts');
  });

  it('exposes generation scripts before test and build', () => {
    const packageJson = JSON.parse(fs.readFileSync(path.join(frontendRoot, 'package.json'), 'utf8')) as {
      scripts: Record<string, string>;
      devDependencies: Record<string, string>;
    };

    expect(packageJson.devDependencies['openapi-typescript']).toBeDefined();
    expect(packageJson.scripts['generate:api']).toBe('node scripts/generate-api-client.mjs');
    expect(packageJson.scripts.pretest).toBe('npm run generate:api');
    expect(packageJson.scripts.prebuild).toBe('npm run generate:api');
  });
});
```

- [ ] **Step 3: Run test and verify RED**

Run:

```powershell
cd outfit_planner_front
npx vitest run src/api/generated/responseTypes.test.ts
cd ..
```

Expected: fails because `.gitignore` and package scripts do not contain the generated workflow yet.

- [ ] **Step 4: Ignore generated artifacts**

Add to `.gitignore`:

```gitignore
outfit_planner_front/.generated/
outfit_planner_front/src/api/generated/schema.ts
```

- [ ] **Step 5: Add generation script**

Create `outfit_planner_front/scripts/generate-api-client.mjs`:

```js
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const frontRoot = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(frontRoot, '..');
const apiProject = path.join(repoRoot, 'outfit_planner_back', 'src', 'OutfitPlanner.Api', 'OutfitPlanner.Api.csproj');
const openApiDir = path.join(frontRoot, '.generated', 'openapi');
const generatedDir = path.join(frontRoot, 'src', 'api', 'generated');
const generatedSchema = path.join(generatedDir, 'schema.ts');

fs.rmSync(openApiDir, { recursive: true, force: true });
fs.mkdirSync(openApiDir, { recursive: true });
fs.mkdirSync(generatedDir, { recursive: true });

execFileSync('dotnet', [
  'build',
  apiProject,
  '/p:OpenApiGenerateDocuments=true',
  `/p:OpenApiDocumentsDirectory=${openApiDir}`
], {
  cwd: repoRoot,
  stdio: 'inherit'
});

const openApiDocument = fs.readdirSync(openApiDir)
  .filter((file) => file.endsWith('.json'))
  .map((file) => path.join(openApiDir, file))
  .sort((left, right) => fs.statSync(right).mtimeMs - fs.statSync(left).mtimeMs)[0];

if (!openApiDocument) {
  throw new Error(`No OpenAPI JSON document was generated in ${openApiDir}.`);
}

const openapiTypescriptBin = path.join(
  frontRoot,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'openapi-typescript.cmd' : 'openapi-typescript'
);

execFileSync(openapiTypescriptBin, [
  openApiDocument,
  '-o',
  generatedSchema
], {
  cwd: frontRoot,
  stdio: 'inherit'
});
```

- [ ] **Step 6: Add package scripts**

Update `outfit_planner_front/package.json` scripts:

```json
{
  "generate:api": "node scripts/generate-api-client.mjs",
  "pretest": "npm run generate:api",
  "prebuild": "npm run generate:api"
}
```

Keep existing scripts unchanged.

- [ ] **Step 7: Add response type aliases**

Create `outfit_planner_front/src/api/generated/responseTypes.ts`:

```ts
import type { paths } from './schema';

type Operation<Path extends keyof paths, Method extends keyof paths[Path]> = paths[Path][Method];

type JsonResponse<TOperation, TStatus extends number> =
  TOperation extends { responses: Record<TStatus, { content: { 'application/json': infer TBody } }> }
    ? TBody
    : never;

type Json200<Path extends keyof paths, Method extends keyof paths[Path]> = JsonResponse<Operation<Path, Method>, 200>;
type Json201<Path extends keyof paths, Method extends keyof paths[Path]> = JsonResponse<Operation<Path, Method>, 201>;
type Json202<Path extends keyof paths, Method extends keyof paths[Path]> = JsonResponse<Operation<Path, Method>, 202>;

export type BodyReferencePhoto = Json200<'/api/body-reference-photos', 'get'>[number];
export type GarmentItem = Json200<'/api/garments', 'get'>[number];
export type Outfit = Json200<'/api/outfits', 'get'>[number];
export type ScheduledOutfit = Json200<'/api/schedule', 'get'>[number];
export type TryOnJob = Json200<'/api/try-on-jobs/{jobId}', 'get'>;
export type SharedOutfit = Json200<'/api/share/{token}', 'get'>;
export type CreatedGarment = Json201<'/api/garments', 'post'>;
export type CreatedBodyReferencePhoto = Json201<'/api/body-reference-photos', 'post'>;
export type CreatedOutfit = Json201<'/api/outfits', 'post'>;
export type StartedTryOnJob = Json202<'/api/outfits/{outfitId}/try-on', 'post'>;

export type GarmentCategory = GarmentItem['category'];
export type BodyZone = GarmentItem['bodyZone'];
export type LaundryStatus = NonNullable<GarmentItem['laundryStatus']>;
export type TryOnStatus = TryOnJob['status'];
export type OutfitItem = Outfit['items'][number];
```

After the first generation, inspect `outfit_planner_front/src/api/generated/schema.ts`. If the path keys differ from the strings above, update only the path string literals in `responseTypes.ts`; keep every exported type alias name unchanged for app consumers.

- [ ] **Step 8: Shrink manual app types**

Update `outfit_planner_front/src/types.ts` to re-export generated API shapes and keep frontend-only state:

```ts
export type {
  BodyReferencePhoto,
  BodyZone,
  GarmentCategory,
  GarmentItem,
  LaundryStatus,
  Outfit,
  OutfitItem,
  ScheduledOutfit,
  TryOnJob,
  TryOnStatus
} from './api/generated/responseTypes';

export type PreviewMode = 'clothes' | 'person';

export interface OutfitSelection {
  topId?: string;
  bottomId?: string;
  dressId?: string;
  outerwearId?: string;
  shoesId?: string;
  bagId?: string;
  accessoryId?: string;
  hatId?: string;
}
```

- [ ] **Step 9: Keep API wrapper behavior**

In `outfit_planner_front/src/api/client.ts`, continue exporting the existing functions and request helpers. Replace the imported model types with generated aliases:

```ts
import type { BodyReferencePhoto, GarmentCategory, GarmentItem, LaundryStatus, Outfit, ScheduledOutfit, TryOnJob } from '../types';
```

Keep this import path unchanged after `types.ts` re-exports generated aliases. Do not replace the `request`, CSRF, upload, or diagnostics implementation in this task.

- [ ] **Step 10: Generate and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm run generate:api
npx vitest run src/api/generated/responseTypes.test.ts
npm run build
cd ..
```

Expected: generator creates ignored `src/api/generated/schema.ts`; workflow test passes; TypeScript build passes.

- [ ] **Step 11: Commit**

Run:

```powershell
git add .gitignore outfit_planner_front\package.json outfit_planner_front\package-lock.json outfit_planner_front\scripts\generate-api-client.mjs outfit_planner_front\src\api\generated\responseTypes.ts outfit_planner_front\src\api\generated\responseTypes.test.ts outfit_planner_front\src\types.ts outfit_planner_front\src\api\client.ts
git commit -m "Generate frontend API types from OpenAPI"
```

## Task 3: Auth Return URL And Route Guard

**Files:**

- Create: `outfit_planner_front/src/features/auth/returnUrl.ts`
- Create: `outfit_planner_front/src/features/auth/returnUrl.test.ts`
- Create: `outfit_planner_front/src/features/auth/authQueries.ts`
- Create: `outfit_planner_front/src/app/RequireAuth.tsx`
- Create: `outfit_planner_front/src/app/RequireAuth.test.tsx`

- [ ] **Step 1: Add failing return URL tests**

Create `outfit_planner_front/src/features/auth/returnUrl.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { buildReturnUrlParam, readSafeReturnUrl } from './returnUrl';

describe('auth return URLs', () => {
  it('builds a returnUrl from current path and search', () => {
    expect(buildReturnUrlParam('/builder', '?tab=tryon')).toBe('/builder?tab=tryon');
  });

  it('falls back when returnUrl is missing unsafe or malformed', () => {
    expect(readSafeReturnUrl(null)).toBe('/builder');
    expect(readSafeReturnUrl('https://evil.test/builder')).toBe('/builder');
    expect(readSafeReturnUrl('//evil.test/builder')).toBe('/builder');
    expect(readSafeReturnUrl('builder')).toBe('/builder');
    expect(readSafeReturnUrl('/signin')).toBe('/builder');
  });

  it('allows internal app return URLs', () => {
    expect(readSafeReturnUrl('/wardrobe')).toBe('/wardrobe');
    expect(readSafeReturnUrl('/calendar?date=2026-06-09')).toBe('/calendar?date=2026-06-09');
  });
});
```

- [ ] **Step 2: Run return URL tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/auth/returnUrl.test.ts
cd ..
```

Expected: fails because `returnUrl.ts` does not exist.

- [ ] **Step 3: Implement return URL helpers**

Create `outfit_planner_front/src/features/auth/returnUrl.ts`:

```ts
const fallbackReturnUrl = '/builder';
const publicAuthPaths = new Set(['/signin', '/register']);

export function buildReturnUrlParam(pathname: string, search = ''): string {
  return `${pathname}${search}`;
}

export function readSafeReturnUrl(value: string | null): string {
  if (!value || !value.startsWith('/') || value.startsWith('//')) {
    return fallbackReturnUrl;
  }

  try {
    const parsed = new URL(value, 'https://outfit-planner.local');
    if (parsed.origin !== 'https://outfit-planner.local') {
      return fallbackReturnUrl;
    }

    if (publicAuthPaths.has(parsed.pathname)) {
      return fallbackReturnUrl;
    }

    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return fallbackReturnUrl;
  }
}
```

- [ ] **Step 4: Run return URL tests and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/auth/returnUrl.test.ts
cd ..
```

Expected: tests pass.

- [ ] **Step 5: Add failing auth guard tests**

Create `outfit_planner_front/src/app/RequireAuth.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RequireAuth } from './RequireAuth';

function renderGuard(initialEntry = '/builder', fetchImpl?: typeof fetch) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  });

  if (fetchImpl) {
    vi.spyOn(globalThis, 'fetch').mockImplementation(fetchImpl);
  }

  function SignInProbe() {
    const location = useLocation();
    return <h1>{`Sign in page ${location.search}`}</h1>;
  }

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route element={<RequireAuth />}>
            <Route path="/builder" element={<h1>Builder private content</h1>} />
            <Route path="/wardrobe" element={<h1>Wardrobe private content</h1>} />
          </Route>
          <Route path="/signin" element={<SignInProbe />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('RequireAuth', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('shows a skeleton while session is loading', () => {
    renderGuard('/builder', () => new Promise<Response>(() => undefined));

    expect(screen.getByLabelText(/loading private page/i)).toBeInTheDocument();
  });

  it('redirects unauthenticated users with a returnUrl', async () => {
    renderGuard('/builder?mode=person', async () => new Response(null, { status: 401 }));

    expect(await screen.findByRole('heading', { name: /returnUrl=%2Fbuilder%3Fmode%3Dperson/i })).toBeInTheDocument();
  });

  it('renders private content for authenticated users', async () => {
    renderGuard('/wardrobe', async () => jsonResponse({
      user: { id: 'user-a', email: 'ada@example.com', displayName: 'Ada' },
      expiresAt: '2026-07-09T12:00:00Z'
    }));

    expect(await screen.findByRole('heading', { name: /wardrobe private content/i })).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

- [ ] **Step 6: Run guard tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/app/RequireAuth.test.tsx
cd ..
```

Expected: fails because `RequireAuth` does not exist.

- [ ] **Step 7: Add auth query hook**

Create `outfit_planner_front/src/features/auth/authQueries.ts`:

```ts
import { useQuery } from '@tanstack/react-query';
import { getCurrentSession } from '../../api/client';

export const authSessionQueryKey = ['auth-session'] as const;

export function useAuthSession() {
  return useQuery({
    queryKey: authSessionQueryKey,
    queryFn: getCurrentSession,
    retry: false
  });
}
```

- [ ] **Step 8: Add route guard**

Create `outfit_planner_front/src/app/RequireAuth.tsx`:

```tsx
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthSession } from '../features/auth/authQueries';
import { buildReturnUrlParam } from '../features/auth/returnUrl';

export function RequireAuth() {
  const location = useLocation();
  const sessionQuery = useAuthSession();

  if (sessionQuery.isLoading || sessionQuery.isPending) {
    return (
      <div className="panel-skeleton" aria-label="Loading private page">
        {Array.from({ length: 5 }, (_, index) => (
          <span key={index} />
        ))}
      </div>
    );
  }

  if (!sessionQuery.data?.user) {
    const returnUrl = buildReturnUrlParam(location.pathname, location.search);
    return <Navigate to={`/signin?returnUrl=${encodeURIComponent(returnUrl)}`} replace />;
  }

  return <Outlet />;
}
```

- [ ] **Step 9: Run guard tests and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/auth/returnUrl.test.ts src/app/RequireAuth.test.tsx
cd ..
```

Expected: return URL and guard tests pass.

- [ ] **Step 10: Commit**

Run:

```powershell
git add outfit_planner_front\src\features\auth\returnUrl.ts outfit_planner_front\src\features\auth\returnUrl.test.ts outfit_planner_front\src\features\auth\authQueries.ts outfit_planner_front\src\app\RequireAuth.tsx outfit_planner_front\src\app\RequireAuth.test.tsx
git commit -m "Add guarded private route foundation"
```

## Task 4: App Entry And Shell Split

**Files:**

- Create: `outfit_planner_front/src/app/App.tsx`
- Create: `outfit_planner_front/src/app/AppShell.tsx`
- Create: `outfit_planner_front/src/features/auth/AuthActions.tsx`
- Create: `outfit_planner_front/src/shared/ui/ClayBlobs.tsx`
- Modify: `outfit_planner_front/src/main.tsx`
- Modify: `outfit_planner_front/src/App.tsx`
- Modify: `outfit_planner_front/src/App.test.tsx`

- [ ] **Step 1: Add failing app import/shell test**

Replace `outfit_planner_front/src/App.test.tsx` with a focused shell compatibility test:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App';

function renderApp(initialEntry = '/share/token-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  });

  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);

    if (url.includes('/auth/providers')) {
      return jsonResponse([]);
    }

    if (url.includes('/auth/me')) {
      return new Response(null, { status: 401 });
    }

    if (url.includes('/share/token-1')) {
      return jsonResponse({
        id: 'outfit-1',
        name: 'Shared clay',
        items: [],
        tags: [],
        occasion: [],
        isFavorite: false,
        isArchived: false,
        clothesOnlyPreviewUrl: null,
        personPreviewUrl: null,
        createdAt: '2026-06-09T12:00:00Z'
      });
    }

    return jsonResponse([]);
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('App shell', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('keeps the compatibility App export and renders public share routes inside the shell', async () => {
    renderApp();

    expect(screen.getByRole('link', { name: /outfit planner/i })).toBeInTheDocument();
    expect(await screen.findByText(/shared clay/i)).toBeInTheDocument();
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
npm test -- src/App.test.tsx
cd ..
```

Expected: fails because `src/app/App.tsx`, route files, and extracted shell do not exist yet.

- [ ] **Step 3: Extract clay ambient component**

Create `outfit_planner_front/src/shared/ui/ClayBlobs.tsx`:

```tsx
export function ClayBlobs() {
  return (
    <div className="clay-ambient" aria-hidden="true">
      <span className="ambient-blob blob-violet" />
      <span className="ambient-blob blob-pink" />
      <span className="ambient-blob blob-blue" />
      <span className="ambient-blob blob-green" />
    </div>
  );
}
```

- [ ] **Step 4: Extract auth sidebar actions**

Create `outfit_planner_front/src/features/auth/AuthActions.tsx` by moving the existing `AuthActions` implementation from `src/App.tsx`. Keep the current labels and CSS classes. The imports should be:

```tsx
import { LogIn, LogOut, ShieldCheck, UserPlus } from 'lucide-react';
import { NavLink } from 'react-router-dom';
import type { AuthUser } from '../../api/client';
```

Keep `headingStyle` local in the file:

```tsx
const headingStyle = { fontFamily: 'Nunito, sans-serif' };
```

- [ ] **Step 5: Add AppShell**

Create `outfit_planner_front/src/app/AppShell.tsx`:

```tsx
import { type CSSProperties, useEffect, useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Shirt, Upload, Wand2 } from 'lucide-react';
import { getAuthProviders, logout } from '../api/client';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { AuthActions } from '../features/auth/AuthActions';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import { ClayBlobs } from '../shared/ui/ClayBlobs';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

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
    <div className="app-shell" data-theme={theme}>
      <ClayBlobs />
      <aside className="sidebar">
        <Link to="/builder" className="brand" style={headingStyle}>
          <span className="brand-orb">
            <Shirt size={26} />
          </span>
          <span>Outfit Planner</span>
        </Link>
        <PrimaryNavigation />
        <AuthActions
          user={sessionQuery.data?.user}
          isSigningOut={logoutMutation.isPending}
          onLogout={() => logoutMutation.mutate()}
        />
        <ThemeToggle theme={theme} onChange={setTheme} />
      </aside>
      <main className="main-panel">
        <Outlet context={{ providers: authProvidersQuery.data ?? [] }} />
      </main>
      <nav className="bottom-navigation" aria-label="Mobile primary navigation">
        <PrimaryNavigation compact />
      </nav>
    </div>
  );
}

function PrimaryNavigation({ compact = false }: { compact?: boolean }) {
  return (
    <nav aria-label={compact ? 'Mobile workspace navigation' : 'Primary navigation'}>
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

If `bottom-navigation` has no CSS yet, add minimal responsive CSS during Task 7 PWA/mobile foundation.

- [ ] **Step 6: Add route tree**

Create `outfit_planner_front/src/app/App.tsx`:

```tsx
import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from './RequireAuth';
import { AppShell } from './AppShell';
import { AuthPage } from '../routes/AuthPage';
import { BuilderPage } from '../routes/BuilderPage';
import { CalendarPage } from '../routes/CalendarPage';
import { SharePage } from '../routes/SharePage';
import { WardrobePage } from '../routes/WardrobePage';

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/signin" element={<AuthPage mode="signin" />} />
        <Route path="/register" element={<AuthPage mode="register" />} />
        <Route path="/share/:token" element={<SharePage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<Navigate to="/builder" replace />} />
          <Route path="/wardrobe" element={<WardrobePage />} />
          <Route path="/builder" element={<BuilderPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
```

- [ ] **Step 7: Keep root App compatibility export**

Replace `outfit_planner_front/src/App.tsx` with:

```tsx
export { default } from './app/App';
```

- [ ] **Step 8: Update main entry**

Change `outfit_planner_front/src/main.tsx` import:

```ts
import App from './app/App';
```

Leave QueryClient and BrowserRouter setup unchanged.

- [ ] **Step 9: Run shell test**

Run:

```powershell
cd outfit_planner_front
npm test -- src/App.test.tsx
cd ..
```

Expected: fails because route files have not been extracted yet. Continue directly to Task 5 before committing the shell split.

## Task 5: Route Pages And Shared UI Extraction

**Files:**

- Create route, feature, and shared UI files listed in File Map.
- Modify imports in the created route pages.
- Modify: `outfit_planner_front/src/App.test.tsx`

- [ ] **Step 1: Add route page regression tests**

Split the old `App.test.tsx` behaviors into route-level tests:

Create `outfit_planner_front/src/routes/WardrobePage.test.tsx` with the old delete garment test and a render helper that wraps `WardrobePage` in QueryClient and MemoryRouter.

Create `outfit_planner_front/src/routes/BuilderPage.test.tsx` with these old tests:

```text
uploads missing wardrobe pieces directly from builder empty slots
deletes body reference photos from the builder controls
does not show the AI try-on consent checkbox in builder controls
renders category choices and real animated mode indicators
```

Create `outfit_planner_front/src/routes/CalendarPage.test.tsx` with:

```text
uses a custom clay date picker instead of the native date input
```

Create `outfit_planner_front/src/routes/SharePage.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SharePage } from './SharePage';

function renderShare() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/share/token-1']}>
        <Routes>
          <Route path="/share/:token" element={<SharePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('SharePage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders a public shared outfit by token', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse({
      id: 'outfit-1',
      name: 'Shared clay',
      items: [],
      clothesOnlyPreviewUrl: null,
      personPreviewUrl: null,
      createdAt: '2026-06-09T12:00:00Z'
    }));

    renderShare();

    expect(await screen.findByText(/shared clay/i)).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

- [ ] **Step 2: Run route tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/WardrobePage.test.tsx src/routes/BuilderPage.test.tsx src/routes/CalendarPage.test.tsx src/routes/SharePage.test.tsx
cd ..
```

Expected: fails because route files do not exist.

- [ ] **Step 3: Extract shared UI**

Move these functions from the old `src/App.tsx` into shared UI files, preserving JSX, class names, labels, and props:

```text
PageHeader -> src/shared/ui/PageHeader.tsx
PanelTitle -> src/shared/ui/PanelTitle.tsx
ClayDatePicker and dateFromIso -> src/shared/ui/ClayDatePicker.tsx
CategorySegmentedControl, GarmentCategoryIcon, BottomsIcon -> src/shared/ui/GarmentCategoryControl.tsx
FilePicker -> src/shared/ui/FilePicker.tsx
MetricOrb -> src/shared/ui/MetricOrb.tsx
EmptyPreview -> src/shared/ui/EmptyPreview.tsx
EmptyState -> src/shared/ui/EmptyState.tsx
SkeletonGrid and PanelSkeleton -> src/shared/ui/Skeletons.tsx
```

Use these export names:

```ts
export function PageHeader(...)
export function PanelTitle(...)
export function ClayDatePicker(...)
export function CategorySegmentedControl(...)
export function GarmentCategoryIcon(...)
export function FilePicker(...)
export function MetricOrb(...)
export function EmptyPreview(...)
export function EmptyState(...)
export function SkeletonGrid(...)
export function PanelSkeleton(...)
```

- [ ] **Step 4: Extract feature components**

Move these functions into feature files, preserving behavior:

```text
GarmentColumn -> src/features/wardrobe/GarmentColumn.tsx
BodyReferenceManager -> src/features/builder/BodyReferenceManager.tsx
SlotPicker -> src/features/builder/SlotPicker.tsx
OutfitList -> src/features/builder/OutfitList.tsx
garmentNameFromFile -> src/features/builder/garmentName.ts
OutfitChoiceList -> src/features/calendar/OutfitChoiceList.tsx
```

Create `outfit_planner_front/src/features/builder/garmentName.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { garmentNameFromFile } from './garmentName';

describe('garmentNameFromFile', () => {
  it('turns file names into readable garment names', () => {
    expect(garmentNameFromFile(new File(['x'], 'linen-shirt.png', { type: 'image/png' }), 'Top')).toBe('linen shirt');
    expect(garmentNameFromFile(new File(['x'], '.png', { type: 'image/png' }), 'Hat')).toBe('Hat');
  });
});
```

- [ ] **Step 5: Extract route pages**

Create route files by moving current page implementations:

```text
AuthPage -> src/routes/AuthPage.tsx
WardrobePage -> src/routes/WardrobePage.tsx
BuilderPage -> src/routes/BuilderPage.tsx
CalendarPage -> src/routes/CalendarPage.tsx
SharePage -> src/routes/SharePage.tsx
```

Each route file should import the shared and feature components it uses. Keep `headingStyle` local in route files that need it:

```ts
const headingStyle = { fontFamily: 'Nunito, sans-serif' };
```

For `AuthPage`, remove the `providers` prop and read providers from the shell outlet context:

```tsx
import { useOutletContext, useSearchParams } from 'react-router-dom';
import type { AuthProvider } from '../api/client';
import { readSafeReturnUrl } from '../features/auth/returnUrl';

export function AuthPage({ mode }: { mode: 'signin' | 'register' }) {
  const { providers } = useOutletContext<{ providers: AuthProvider[] }>();
  const [searchParams] = useSearchParams();
  const returnUrl = readSafeReturnUrl(searchParams.get('returnUrl'));
  // use returnUrl in navigate(returnUrl) and buildExternalAuthUrl(provider, returnUrl)
}
```

- [ ] **Step 6: Update AuthPage success navigation**

In `AuthPage`, update mutation success:

```ts
onSuccess: (session) => {
  queryClient.setQueryData(authSessionQueryKey, session);
  void queryClient.invalidateQueries();
  navigate(returnUrl);
}
```

Update external auth buttons:

```tsx
onClick={() => window.location.assign(buildExternalAuthUrl('google', returnUrl))}
onClick={() => window.location.assign(buildExternalAuthUrl('apple', returnUrl))}
```

- [ ] **Step 7: Run route and shell tests**

Run:

```powershell
cd outfit_planner_front
npm test -- src/App.test.tsx src/routes/WardrobePage.test.tsx src/routes/BuilderPage.test.tsx src/routes/CalendarPage.test.tsx src/routes/SharePage.test.tsx src/features/builder/garmentName.test.ts
cd ..
```

Expected: all route extraction tests pass.

- [ ] **Step 8: Commit**

Run:

```powershell
git add outfit_planner_front\src\App.tsx outfit_planner_front\src\App.test.tsx outfit_planner_front\src\app outfit_planner_front\src\routes outfit_planner_front\src\features\auth outfit_planner_front\src\features\builder outfit_planner_front\src\features\calendar outfit_planner_front\src\features\wardrobe outfit_planner_front\src\shared\ui outfit_planner_front\src\main.tsx
git commit -m "Split frontend app shell and route pages"
```

## Task 6: Builder Stale Active Outfit Fix

**Files:**

- Modify: `outfit_planner_front/src/routes/BuilderPage.tsx`
- Modify: `outfit_planner_front/src/routes/BuilderPage.test.tsx`

- [ ] **Step 1: Add failing stale active outfit test**

Add this test to `BuilderPage.test.tsx`:

```tsx
it('clears the active saved outfit when the draft selection changes', async () => {
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);

    if (url.endsWith('/garments')) {
      return jsonResponse([
        {
          id: 'top-1',
          name: 'white tee',
          category: 'Top',
          bodyZone: 'Torso',
          imageUrl: 'http://localhost:5000/uploads/garments/white.png',
          thumbnailUrl: 'http://localhost:5000/uploads/garments/white.png',
          tags: [],
          createdAt: '2026-06-09T12:00:00Z'
        },
        {
          id: 'top-2',
          name: 'black tee',
          category: 'Top',
          bodyZone: 'Torso',
          imageUrl: 'http://localhost:5000/uploads/garments/black.png',
          thumbnailUrl: 'http://localhost:5000/uploads/garments/black.png',
          tags: [],
          createdAt: '2026-06-09T12:00:00Z'
        }
      ]);
    }

    if (url.endsWith('/outfits')) {
      return jsonResponse([
        {
          id: 'outfit-1',
          name: 'Saved outfit',
          items: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: 'http://localhost:5000/uploads/garments/white.png' }],
          createdAt: '2026-06-09T12:00:00Z'
        }
      ]);
    }

    return jsonResponse([]);
  });

  renderBuilder();

  await userEvent.click(await screen.findByRole('button', { name: /saved outfit/i }));
  expect(screen.getByRole('button', { name: /share/i })).not.toBeDisabled();

  await userEvent.click(await screen.findByRole('button', { name: /black tee/i }));

  expect(screen.getByRole('button', { name: /share/i })).toBeDisabled();
});
```

Use the existing route test render helper and `jsonResponse` helper.

- [ ] **Step 2: Run test and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/BuilderPage.test.tsx
cd ..
```

Expected: stale active outfit test fails because Share remains enabled after selection changes.

- [ ] **Step 3: Implement draft selection invalidation**

In `BuilderPage`, add a helper:

```ts
function updateSelection(selectionKey: keyof OutfitSelection, id: string) {
  setSelection((current) => {
    if (current[selectionKey] === id) {
      return current;
    }

    return { ...current, [selectionKey]: id };
  });
  setActiveOutfit(null);
}
```

Update `SlotPicker` usage:

```tsx
onSelect={(id) => updateSelection(selectionKey, id)}
```

Update quick-add success to clear stale active outfit too:

```ts
onSuccess: (garment) => {
  setSelection((current) => ({
    ...current,
    [CATEGORY_SELECTION_KEYS[garment.category]]: garment.id
  }));
  setActiveOutfit(null);
  setQuickAddGarmentError(null);
  void queryClient.invalidateQueries({ queryKey: ['garments'] });
}
```

Do not clear `activeOutfit` inside `saveMutation.onSuccess`; saving should set the new active outfit.

- [ ] **Step 4: Run Builder tests and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/BuilderPage.test.tsx
cd ..
```

Expected: all Builder tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add outfit_planner_front\src\routes\BuilderPage.tsx outfit_planner_front\src\routes\BuilderPage.test.tsx
git commit -m "Clear stale active outfit on draft changes"
```

## Task 7: PWA And Mobile Navigation Foundation

**Files:**

- Create: `outfit_planner_front/public/manifest.webmanifest`
- Create: `outfit_planner_front/public/offline.html`
- Create: `outfit_planner_front/public/sw.js`
- Create: `outfit_planner_front/public/icons/outfit-icon.svg`
- Create: `outfit_planner_front/src/app/registerServiceWorker.ts`
- Create: `outfit_planner_front/src/app/pwa.test.ts`
- Modify: `outfit_planner_front/index.html`
- Modify: `outfit_planner_front/src/main.tsx`
- Modify: `outfit_planner_front/src/styles.css`

- [ ] **Step 1: Add failing PWA smoke tests**

Create `outfit_planner_front/src/app/pwa.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { registerServiceWorker } from './registerServiceWorker';

const frontendRoot = path.resolve(__dirname, '../..');

describe('PWA foundation', () => {
  it('links a web manifest from index.html', () => {
    const index = fs.readFileSync(path.join(frontendRoot, 'index.html'), 'utf8');

    expect(index).toContain('<link rel="manifest" href="/manifest.webmanifest" />');
    expect(index).toContain('<meta name="theme-color" content="#F4F1FA" />');
  });

  it('defines installable app metadata', () => {
    const manifest = JSON.parse(fs.readFileSync(path.join(frontendRoot, 'public', 'manifest.webmanifest'), 'utf8')) as {
      name: string;
      short_name: string;
      display: string;
      start_url: string;
      icons: Array<{ src: string }>;
    };

    expect(manifest.name).toBe('Outfit Planner');
    expect(manifest.short_name).toBe('Outfits');
    expect(manifest.display).toBe('standalone');
    expect(manifest.start_url).toBe('/builder');
    expect(manifest.icons.some((icon) => icon.src.includes('/icons/outfit-icon.svg'))).toBe(true);
  });

  it('registers the service worker only when supported', async () => {
    const register = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { serviceWorker: { register } });

    await registerServiceWorker();

    expect(register).toHaveBeenCalledWith('/sw.js');
  });
});
```

- [ ] **Step 2: Run PWA tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/app/pwa.test.ts
cd ..
```

Expected: fails because PWA files and registration function do not exist.

- [ ] **Step 3: Add manifest**

Create `outfit_planner_front/public/manifest.webmanifest`:

```json
{
  "name": "Outfit Planner",
  "short_name": "Outfits",
  "description": "Plan outfits from your wardrobe, calendar, and try-on previews.",
  "start_url": "/builder",
  "scope": "/",
  "display": "standalone",
  "background_color": "#F4F1FA",
  "theme_color": "#F4F1FA",
  "icons": [
    {
      "src": "/icons/outfit-icon.svg",
      "sizes": "any",
      "type": "image/svg+xml",
      "purpose": "any maskable"
    }
  ]
}
```

- [ ] **Step 4: Add icon and offline page**

Create `outfit_planner_front/public/icons/outfit-icon.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-label="Outfit Planner">
  <rect width="512" height="512" rx="112" fill="#F4F1FA"/>
  <circle cx="176" cy="176" r="86" fill="#C7B8FF"/>
  <circle cx="336" cy="184" r="72" fill="#9BD9E7"/>
  <path d="M150 306c32-32 180-34 212 0 24 26 19 88-21 111-38 22-146 23-184 0-38-23-32-85-7-111Z" fill="#FF9FCB"/>
  <path d="M214 120h84l21 70-63 47-63-47 21-70Z" fill="#FFFFFF" opacity=".92"/>
</svg>
```

Create `outfit_planner_front/public/offline.html`:

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Outfit Planner Offline</title>
    <style>
      body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: system-ui, sans-serif; background: #f4f1fa; color: #29243a; }
      main { max-width: 28rem; padding: 2rem; text-align: center; }
    </style>
  </head>
  <body>
    <main>
      <h1>Outfit Planner is offline</h1>
      <p>The app shell is installed. Reconnect to sync wardrobe and outfit data.</p>
    </main>
  </body>
</html>
```

- [ ] **Step 5: Add service worker**

Create `outfit_planner_front/public/sw.js`:

```js
const CACHE_NAME = 'outfit-planner-shell-v1';
const SHELL_ASSETS = ['/', '/builder', '/offline.html', '/manifest.webmanifest', '/icons/outfit-icon.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(
      keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
    ))
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return;
  }

  const requestUrl = new URL(event.request.url);
  if (requestUrl.pathname.startsWith('/api/') || requestUrl.pathname.startsWith('/uploads/')) {
    return;
  }

  event.respondWith(
    caches.match(event.request).then((cached) => cached ?? fetch(event.request).catch(() => {
      if (event.request.mode === 'navigate') {
        return caches.match('/offline.html');
      }

      return Response.error();
    }))
  );
});
```

- [ ] **Step 6: Add guarded registration**

Create `outfit_planner_front/src/app/registerServiceWorker.ts`:

```ts
export async function registerServiceWorker() {
  if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }

  try {
    await navigator.serviceWorker.register('/sw.js');
  } catch (error) {
    console.info('[OutfitPlanner PWA] Service worker registration failed', error);
  }
}
```

Update `main.tsx` after render:

```ts
import { registerServiceWorker } from './app/registerServiceWorker';

void registerServiceWorker();
```

- [ ] **Step 7: Link manifest**

Update `outfit_planner_front/index.html` head:

```html
<meta name="theme-color" content="#F4F1FA" />
<link rel="manifest" href="/manifest.webmanifest" />
```

- [ ] **Step 8: Add responsive bottom navigation CSS**

Append to `outfit_planner_front/src/styles.css` near navigation rules:

```css
.bottom-navigation {
  display: none;
}

@media (max-width: 760px) {
  .bottom-navigation {
    position: fixed;
    left: 12px;
    right: 12px;
    bottom: 12px;
    z-index: 20;
    display: block;
    border-radius: 24px;
    background: color-mix(in srgb, var(--panel) 88%, transparent);
    box-shadow: var(--shadow-convex);
    backdrop-filter: blur(18px);
  }

  .bottom-navigation nav {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 6px;
    padding: 8px;
  }

  .bottom-navigation a {
    min-height: 48px;
    justify-content: center;
    text-align: center;
  }

  .main-panel {
    padding-bottom: 88px;
  }
}
```

Adjust CSS variable names if existing stylesheet uses a different token for panels or shadows.

- [ ] **Step 9: Run PWA test and build**

Run:

```powershell
cd outfit_planner_front
npm test -- src/app/pwa.test.ts
npm run build
cd ..
```

Expected: PWA tests pass and build includes public manifest/service worker assets.

- [ ] **Step 10: Commit**

Run:

```powershell
git add outfit_planner_front\index.html outfit_planner_front\public outfit_planner_front\src\app\registerServiceWorker.ts outfit_planner_front\src\app\pwa.test.ts outfit_planner_front\src\main.tsx outfit_planner_front\src\styles.css
git commit -m "Add PWA shell foundation"
```

## Task 8: Auth Page Return Flow Tests

**Files:**

- Create or modify: `outfit_planner_front/src/routes/AuthPage.test.tsx`
- Modify: `outfit_planner_front/src/routes/AuthPage.tsx`

- [ ] **Step 1: Add failing AuthPage return URL tests**

Create `outfit_planner_front/src/routes/AuthPage.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthPageContent } from './AuthPage';
import type { AuthProvider } from '../api/client';

function renderAuth(returnUrl: string, mode: 'signin' | 'register') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const providers: AuthProvider[] = [
    { id: 'email', label: 'Email', configured: true, flow: 'password' },
    { id: 'google', label: 'Google', configured: false, flow: 'oauth' },
    { id: 'apple', label: 'Apple', configured: false, flow: 'oidc' }
  ];

  vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse({
    user: { id: 'user-a', email: 'ada@example.com', displayName: 'Ada' },
    expiresAt: '2026-07-09T12:00:00Z'
  }));

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/signin']}>
        <Routes>
          <Route path="/signin" element={<AuthPageContent mode={mode} providers={providers} returnUrl={returnUrl} />} />
          <Route path="/register" element={<AuthPageContent mode={mode} providers={providers} returnUrl={returnUrl} />} />
          <Route path="/builder" element={<h1>Builder target</h1>} />
          <Route path="/wardrobe" element={<h1>Wardrobe target</h1>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('AuthPageContent', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('returns to the requested internal URL after sign in', async () => {
    renderAuth('/wardrobe', 'signin');

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/password/i), 'abc12345');
    await userEvent.click(screen.getByRole('button', { name: /^sign in$/i }));

    expect(await screen.findByRole('heading', { name: /wardrobe target/i })).toBeInTheDocument();
  });

  it('falls back to builder for unsafe return URLs', async () => {
    renderAuth('/builder', 'signin');

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/password/i), 'abc12345');
    await userEvent.click(screen.getByRole('button', { name: /^sign in$/i }));

    expect(await screen.findByRole('heading', { name: /builder target/i })).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

- [ ] **Step 2: Run AuthPage tests and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/AuthPage.test.tsx
cd ..
```

Expected: fails because `AuthPageContent` is not exported yet.

- [ ] **Step 3: Make AuthPage testable**

Split `AuthPage.tsx` into a route wrapper and a testable content component:

```tsx
export function AuthPage({ mode }: { mode: 'signin' | 'register' }) {
  const { providers } = useOutletContext<{ providers: AuthProvider[] }>();
  const [searchParams] = useSearchParams();

  return (
    <AuthPageContent
      mode={mode}
      providers={providers}
      returnUrl={readSafeReturnUrl(searchParams.get('returnUrl'))}
    />
  );
}

export function AuthPageContent({
  mode,
  providers,
  returnUrl
}: {
  mode: 'signin' | 'register';
  providers: AuthProvider[];
  returnUrl: string;
}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ email: '', password: '', repeatPassword: '' });
  const authMutation = useMutation({
    mutationFn: () => mode === 'register'
      ? register({ email: form.email, password: form.password, repeatPassword: form.repeatPassword })
      : login({ email: form.email, password: form.password }),
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
      void queryClient.invalidateQueries();
      navigate(returnUrl);
    }
  });
}
```

After adding the wrapper and mutation above, copy the return block from the existing `src/App.tsx` `function AuthPage`, starting at `<section className="auth-page">` and ending at its matching closing `</section>`, into `AuthPageContent`. Preserve labels, classes, provider button rendering, and validation attributes.

In the moved external auth button handlers, use:

```tsx
onClick={() => window.location.assign(buildExternalAuthUrl('google', returnUrl))}
onClick={() => window.location.assign(buildExternalAuthUrl('apple', returnUrl))}
```

- [ ] **Step 4: Run AuthPage tests and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/AuthPage.test.tsx src/features/auth/returnUrl.test.ts
cd ..
```

Expected: Auth return flow tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add outfit_planner_front\src\routes\AuthPage.tsx outfit_planner_front\src\routes\AuthPage.test.tsx
git commit -m "Return users to requested page after auth"
```

## Task 9: Optional Playwright E2E Smoke

**Files:**

- Create: `outfit_planner_front/playwright.config.ts`
- Create: `outfit_planner_front/e2e/register-upload-plan-share.spec.ts`
- Modify: `outfit_planner_front/package.json`
- Modify: `outfit_planner_front/package-lock.json`

- [ ] **Step 1: Decide based on dependency/install budget**

If the implementation session can install one more test dependency, run:

```powershell
cd outfit_planner_front
npm install --save-dev @playwright/test
npx playwright install chromium
cd ..
```

If browser installation fails, record the exact error and skip the remaining Task 9 steps. Continue to Task 10.

- [ ] **Step 2: Add Playwright config**

Create `outfit_planner_front/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  retries: 0,
  use: {
    baseURL: 'https://localhost:5173',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
```

Add package script:

```json
"test:e2e": "playwright test"
```

- [ ] **Step 3: Add e2e smoke skeleton**

Create `outfit_planner_front/e2e/register-upload-plan-share.spec.ts`:

```ts
import { expect, test } from '@playwright/test';

test('register upload create try-on plan and share smoke', async ({ page }) => {
  const email = `ada-${Date.now()}@example.test`;

  await page.goto('/register');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/^password$/i).fill('abc12345');
  await page.getByLabel(/repeat password/i).fill('abc12345');
  await page.getByRole('button', { name: /^register$/i }).click();

  await expect(page).toHaveURL(/\/builder/);
  await page.goto('/wardrobe');

  const fileInput = page.getByLabel(/garment photo/i);
  await fileInput.setInputFiles({
    name: 'linen-shirt.png',
    mimeType: 'image/png',
    buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/j///9/AAn7A/0FQ0XKAAAAAElFTkSuQmCC', 'base64')
  });
  await page.getByLabel(/^name$/i).fill('linen shirt');
  await page.getByRole('button', { name: /add piece/i }).click();
  await expect(page.getByText(/linen shirt/i)).toBeVisible();

  await page.goto('/builder');
  await page.getByRole('button', { name: /linen shirt/i }).click();
  await page.getByLabel(/outfit name/i).fill('Smoke outfit');
  await page.getByRole('button', { name: /save outfit/i }).click();
  await expect(page.getByRole('button', { name: /share/i })).toBeEnabled();

  await page.goto('/calendar');
  await page.getByRole('radio', { name: /smoke outfit/i }).click();
  await page.getByRole('button', { name: /plan day/i }).click();
  await expect(page.getByText(/smoke outfit/i)).toBeVisible();

  await page.goto('/builder');
  await page.getByRole('button', { name: /smoke outfit/i }).click();
  await page.getByRole('button', { name: /share/i }).click();
  await expect(page.getByRole('link', { name: /share\//i })).toBeVisible();
});
```

If existing labels differ after extraction, update the test selectors to the exact accessible names in the UI.

- [ ] **Step 4: Run e2e only with dev stack available**

Start backend and frontend in separate terminals:

```powershell
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
cd outfit_planner_front
npm run dev
```

Then run:

```powershell
cd outfit_planner_front
npm run test:e2e
cd ..
```

Expected: e2e passes against in-memory backend and mock try-on provider. If server startup or browser install fails, record the blocker in final verification.

- [ ] **Step 5: Commit if Task 9 completed**

Run:

```powershell
git add outfit_planner_front\package.json outfit_planner_front\package-lock.json outfit_planner_front\playwright.config.ts outfit_planner_front\e2e
git commit -m "Add Playwright smoke flow"
```

## Task 10: Documentation And Final Verification

**Files:**

- Modify: `README.md`
- Modify: `agents.md`

- [ ] **Step 1: Update README layout**

In `README.md`, update the frontend repository layout block so `outfit_planner_front/src/` includes:

```text
        |-- app/
        |-- routes/
        |-- api/
        |-- components/
        |-- features/
        |-- shared/
        `-- types.ts
```

- [ ] **Step 2: Update README frontend commands**

Add under Local Frontend Development:

```markdown
Generate frontend API types from the backend OpenAPI document:

```powershell
cd outfit_planner_front
npm run generate:api
```

`npm test` and `npm run build` run this generation step first. Generated OpenAPI and TypeScript schema files are local build artifacts and are not committed.
```

- [ ] **Step 3: Update README feature notes**

Add to Features:

```markdown
- Installable PWA shell with manifest metadata, static shell caching, and an offline fallback page.
```

Fix the stale Current Boundaries bullet:

```markdown
- Garment categories are Top, Bottom, Dress, Outerwear, Shoes, Bag, Accessory, and Hat.
```

- [ ] **Step 4: Update agents.md durable context**

Add a concise frontend architecture note:

```markdown
- Frontend app composition is split across `src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable clay UI under `src/shared/ui`.
- Frontend generated OpenAPI artifacts live under ignored paths and should be regenerated with `npm run generate:api`, not committed.
```

- [ ] **Step 5: Run full frontend tests**

Run:

```powershell
cd outfit_planner_front
npm test
cd ..
```

Expected: all Vitest tests pass.

- [ ] **Step 6: Run frontend build**

Run:

```powershell
cd outfit_planner_front
npm run build
cd ..
```

Expected: TypeScript and Vite build pass.

- [ ] **Step 7: Run backend verification**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: backend console tests and API build pass.

- [ ] **Step 8: Verify generated files are untracked**

Run:

```powershell
git status --short
```

Expected: generated files under `outfit_planner_front/.generated/` and `outfit_planner_front/src/api/generated/schema.ts` do not appear. Only intentional source/doc/package changes appear.

- [ ] **Step 9: Run Playwright e2e if Task 9 completed**

Run:

```powershell
cd outfit_planner_front
npm run test:e2e
cd ..
```

Expected: e2e passes. If not run, record the exact reason, such as "Playwright browser install failed" or "dev stack could not start".

- [ ] **Step 10: Commit docs and final integration fixes**

Run:

```powershell
git add README.md agents.md
git commit -m "Document frontend foundation workflow"
```

If final verification required small fixes, include those files in the commit with a message that names the fix.

## Final Completion Checklist

- [ ] Private routes use `RequireAuth`.
- [ ] `/signin?returnUrl=...` returns users to safe internal paths after auth.
- [ ] `src/App.tsx` is no longer the monolith.
- [ ] Existing Wardrobe/Builder/Calendar/Auth/Share flows pass route tests.
- [ ] Builder clears stale active saved outfit on draft changes.
- [ ] Backend OpenAPI generation exists and is tested.
- [ ] Frontend generated schema is ignored, regenerated by scripts, and consumed by committed type aliases.
- [ ] PWA manifest, service worker, offline page, and mobile bottom navigation exist.
- [ ] README and `agents.md` reflect the new structure.
- [ ] `npm test`, `npm run build`, backend tests, and backend build have run.
- [ ] Playwright e2e has either passed or the blocker is reported.
