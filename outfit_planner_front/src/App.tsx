import { type ChangeEvent, type CSSProperties, type ReactNode, useEffect, useMemo, useState } from 'react';
import { Link, NavLink, Route, Routes, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addMonths, format, isToday, subMonths } from 'date-fns';
import {
  CalendarDays,
  Camera,
  Check,
  ChevronLeft,
  ChevronRight,
  Eye,
  GitBranch,
  Heart,
  ImagePlus,
  Layers3,
  Link2,
  LogIn,
  LogOut,
  Plus,
  ShieldCheck,
  ScanFace,
  Shirt,
  Sparkles,
  Trash2,
  UserPlus,
  Upload,
  Wand2
} from 'lucide-react';
import {
  buildExternalAuthUrl,
  createBodyReferencePhoto,
  createGarment,
  createOutfit,
  deleteBodyReferencePhoto,
  deleteGarment,
  getCurrentSession,
  getAuthProviders,
  getSharedOutfit,
  getTryOnJob,
  listBodyReferencePhotos,
  listGarments,
  listOutfits,
  listSchedule,
  login,
  logout,
  register,
  scheduleOutfit,
  shareOutfit,
  startTryOn,
  uploadBodyReferencePhoto,
  uploadGarmentPhoto
} from './api/client';
import type { AuthProvider, AuthUser } from './api/client';
import { ModeToggle } from './components/ModeToggle';
import { ThemeToggle, type ThemeMode } from './components/ThemeToggle';
import { buildMonthCalendar, weekDayLabels } from './features/calendar/calendarUtils';
import { groupGarmentsByCategory, selectedGarmentIds, selectionLabel } from './features/outfits/outfitUtils';
import { validateUploadImageFile } from './features/uploads/imageFile';
import type { BodyReferencePhoto, GarmentCategory, GarmentItem, Outfit, OutfitSelection, PreviewMode } from './types';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

function App() {
  const queryClient = useQueryClient();
  const [theme, setTheme] = useState<ThemeMode>(() => {
    const storedTheme = localStorage.getItem('outfit-planner-theme');
    return storedTheme === 'dark' ? 'dark' : 'light';
  });
  const authProvidersQuery = useQuery({ queryKey: ['auth-providers'], queryFn: getAuthProviders, retry: 1 });
  const sessionQuery = useQuery({ queryKey: ['auth-session'], queryFn: getCurrentSession, retry: false });
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(['auth-session'], null);
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
        <nav aria-label="Primary navigation">
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
        <AuthActions
          user={sessionQuery.data?.user}
          isSigningOut={logoutMutation.isPending}
          onLogout={() => logoutMutation.mutate()}
        />
        <ThemeToggle theme={theme} onChange={setTheme} />
      </aside>
      <main className="main-panel">
        <Routes>
          <Route path="/" element={<BuilderPage />} />
          <Route path="/signin" element={<AuthPage mode="signin" providers={authProvidersQuery.data ?? []} />} />
          <Route path="/register" element={<AuthPage mode="register" providers={authProvidersQuery.data ?? []} />} />
          <Route path="/wardrobe" element={<WardrobePage />} />
          <Route path="/builder" element={<BuilderPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="/share/:token" element={<SharePage />} />
        </Routes>
      </main>
    </div>
  );
}

function ClayBlobs() {
  return (
    <div className="clay-ambient" aria-hidden="true">
      <span className="ambient-blob blob-violet" />
      <span className="ambient-blob blob-pink" />
      <span className="ambient-blob blob-blue" />
      <span className="ambient-blob blob-green" />
    </div>
  );
}

function AuthActions({
  user,
  isSigningOut,
  onLogout
}: {
  user?: AuthUser | null;
  isSigningOut: boolean;
  onLogout: () => void;
}) {
  if (user) {
    return (
      <section className="auth-actions signed-in" aria-label="Account">
        <div className="auth-user-pill">
          <span>
            <ShieldCheck size={17} />
          </span>
          <div>
            <small style={headingStyle}>Signed in</small>
            <strong style={headingStyle}>{user.email ?? user.displayName}</strong>
          </div>
        </div>
        <button type="button" className="auth-nav-action" disabled={isSigningOut} onClick={onLogout}>
          <LogOut size={17} />
          <span>{isSigningOut ? 'Signing out' : 'Sign out'}</span>
        </button>
      </section>
    );
  }

  return (
    <section className="auth-actions" aria-label="Authentication">
      <NavLink to="/signin" className="auth-nav-action">
        <LogIn size={17} />
        <span>Sign in</span>
      </NavLink>
      <NavLink to="/register" className="auth-nav-action register-action">
        <UserPlus size={17} />
        <span>Register</span>
      </NavLink>
    </section>
  );
}

function AuthPage({ mode, providers }: { mode: 'signin' | 'register'; providers: AuthProvider[] }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ email: '', password: '', repeatPassword: '' });
  const authMutation = useMutation({
    mutationFn: () => mode === 'register'
      ? register({ email: form.email, password: form.password, repeatPassword: form.repeatPassword })
      : login({ email: form.email, password: form.password }),
    onSuccess: (session) => {
      queryClient.setQueryData(['auth-session'], session);
      void queryClient.invalidateQueries();
      navigate('/builder');
    }
  });
  const title = mode === 'register' ? 'Register' : 'Sign in';
  const alternate = mode === 'register'
    ? { to: '/signin', label: 'Sign in' }
    : { to: '/register', label: 'Register' };
  const googleProvider = providers.find((provider) => provider.id === 'google');
  const appleProvider = providers.find((provider) => provider.id === 'apple');

  return (
    <section className="auth-page">
      <div className="auth-card">
        <PanelTitle icon={mode === 'register' ? <UserPlus size={19} /> : <LogIn size={19} />} title={title} />
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            authMutation.mutate();
          }}
        >
          <label>
            <span>Email</span>
            <input
              type="email"
              autoComplete="email"
              value={form.email}
              onChange={(event) => setForm({ ...form, email: event.target.value })}
              required
            />
          </label>
          <label>
            <span>Password</span>
            <input
              type="password"
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
              minLength={8}
              pattern={mode === 'register' ? '^(?=.*[A-Za-z])(?=.*\\d).{8,}$' : undefined}
              title={mode === 'register' ? 'Use at least 8 characters with at least one letter and one digit.' : undefined}
              value={form.password}
              onChange={(event) => setForm({ ...form, password: event.target.value })}
              required
            />
          </label>
          {mode === 'register' ? (
            <label>
              <span>Repeat password</span>
              <input
                type="password"
                autoComplete="new-password"
                minLength={8}
                pattern="^(?=.*[A-Za-z])(?=.*\d).{8,}$"
                title="Use at least 8 characters with at least one letter and one digit."
                value={form.repeatPassword}
                onChange={(event) => setForm({ ...form, repeatPassword: event.target.value })}
                required
              />
            </label>
          ) : null}
          <button type="submit" className="clay-button primary-action" disabled={authMutation.isPending}>
            {mode === 'register' ? <UserPlus size={16} /> : <LogIn size={16} />}
            {authMutation.isPending ? 'Working' : title}
          </button>
          {authMutation.error ? <p className="error">{authMutation.error.message}</p> : null}
        </form>

        <div className="external-auth-actions">
          <button
            type="button"
            className="oauth-button"
            disabled={!googleProvider?.configured}
            onClick={() => window.location.assign(buildExternalAuthUrl('google', '/builder'))}
          >
            <span>G</span>
            Google
          </button>
          <button
            type="button"
            className="oauth-button"
            disabled={!appleProvider?.configured}
            onClick={() => window.location.assign(buildExternalAuthUrl('apple', '/builder'))}
          >
            <span>A</span>
            Apple
          </button>
        </div>

        <Link className="auth-switch-link" to={alternate.to}>
          {alternate.label}
        </Link>
      </div>
    </section>
  );
}

function WardrobePage() {
  const queryClient = useQueryClient();
  const garmentsQuery = useQuery({ queryKey: ['garments'], queryFn: listGarments });
  const [form, setForm] = useState({
    name: '',
    category: 'Top' as GarmentCategory,
    tags: ''
  });
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreviewUrl, setPhotoPreviewUrl] = useState('');
  const [fileInputKey, setFileInputKey] = useState(0);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const createMutation = useMutation({
    mutationFn: async (input: { name: string; category: GarmentCategory; photo: File; tags: string[] }) => {
      const uploadedPhoto = await uploadGarmentPhoto(input.photo);
      return await createGarment({
        name: input.name,
        category: input.category,
        imageUrl: uploadedPhoto.url,
        thumbnailUrl: uploadedPhoto.url,
        tags: input.tags
      });
    },
    onSuccess: () => {
      setForm({ name: '', category: 'Top', tags: '' });
      setPhotoFile(null);
      setPhotoPreviewUrl('');
      setFileInputKey((key) => key + 1);
      setUploadError(null);
      void queryClient.invalidateQueries({ queryKey: ['garments'] });
    }
  });
  const deleteGarmentMutation = useMutation({
    mutationFn: deleteGarment,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['garments'] });
    }
  });
  const garments = garmentsQuery.data ?? [];
  const grouped = groupGarmentsByCategory(garments);

  useEffect(() => {
    return () => {
      if (photoPreviewUrl) {
        URL.revokeObjectURL(photoPreviewUrl);
      }
    };
  }, [photoPreviewUrl]);

  return (
    <section className="page-grid wardrobe-view">
      <div className="workspace">
        <PageHeader
          eyebrow="Wardrobe"
          title="Shape a tactile closet from your photos"
          text="Upload clean garment photos, keep categories strict, and build outfits from pieces that feel ready to touch."
        />
        <div className="wardrobe-summary">
          <MetricOrb label="Tops" value={grouped.Top.length} tone="violet" />
          <MetricOrb label="Bottoms" value={grouped.Bottom.length} tone="blue" />
          <MetricOrb label="Pieces" value={garments.length} tone="pink" />
        </div>
        {garmentsQuery.isLoading ? (
          <SkeletonGrid />
        ) : (
          <div className="wardrobe-columns">
            <GarmentColumn
              title="Tops"
              items={grouped.Top}
              deletingId={deleteGarmentMutation.isPending ? deleteGarmentMutation.variables : undefined}
              onDelete={(id) => deleteGarmentMutation.mutate(id)}
            />
            <GarmentColumn
              title="Bottoms"
              items={grouped.Bottom}
              deletingId={deleteGarmentMutation.isPending ? deleteGarmentMutation.variables : undefined}
              onDelete={(id) => deleteGarmentMutation.mutate(id)}
            />
          </div>
        )}
        {deleteGarmentMutation.error ? <p className="error">{deleteGarmentMutation.error.message}</p> : null}
      </div>
      <aside className="tool-panel add-garment-panel">
        <PanelTitle icon={<ImagePlus size={19} />} title="Add garment" />
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            if (!photoFile) {
              setUploadError('Choose a JPG, PNG, or WebP photo from your device.');
              return;
            }

            createMutation.mutate({
              name: form.name,
              category: form.category,
              photo: photoFile,
              tags: form.tags.split(',').map((tag) => tag.trim()).filter(Boolean)
            });
          }}
        >
          <label>
            <span>Name</span>
            <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
          </label>
          <CategorySegmentedControl
            value={form.category}
            onChange={(category) => setForm({ ...form, category })}
          />
          <FilePicker
            key={fileInputKey}
            label="Garment photo"
            fileName={photoFile?.name}
            onChange={(file) => {
              if (!file) {
                setPhotoFile(null);
                setPhotoPreviewUrl('');
                return;
              }

              try {
                validateUploadImageFile(file);
              } catch (error) {
                setPhotoFile(null);
                setPhotoPreviewUrl('');
                setUploadError((error as Error).message);
                return;
              }

              if (photoPreviewUrl) {
                URL.revokeObjectURL(photoPreviewUrl);
              }

              setPhotoFile(file);
              setPhotoPreviewUrl(URL.createObjectURL(file));
              setUploadError(null);
            }}
          />
          {photoPreviewUrl ? (
            <div className="upload-preview">
              <img src={photoPreviewUrl} alt={`${form.name || 'Garment'} upload preview`} />
              <span>{photoFile?.name}</span>
            </div>
          ) : null}
          <label>
            <span>Tags</span>
            <input value={form.tags} onChange={(event) => setForm({ ...form, tags: event.target.value })} />
          </label>
          <button type="submit" className="clay-button primary-action" disabled={createMutation.isPending || !photoFile}>
            <Plus size={16} />
            {createMutation.isPending ? 'Uploading' : 'Add piece'}
          </button>
          {uploadError ? <p className="error">{uploadError}</p> : null}
          {createMutation.error ? <p className="error">{createMutation.error.message}</p> : null}
        </form>
      </aside>
    </section>
  );
}

function BuilderPage() {
  const queryClient = useQueryClient();
  const garmentsQuery = useQuery({ queryKey: ['garments'], queryFn: listGarments });
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const bodyPhotosQuery = useQuery({ queryKey: ['body-reference-photos'], queryFn: listBodyReferencePhotos });
  const garments = garmentsQuery.data ?? [];
  const grouped = groupGarmentsByCategory(garments);
  const [selection, setSelection] = useState<OutfitSelection>({});
  const [mode, setMode] = useState<PreviewMode>('clothes');
  const [outfitName, setOutfitName] = useState('Today');
  const [selectedBodyPhotoId, setSelectedBodyPhotoId] = useState('');
  const [bodyPhotoUploadError, setBodyPhotoUploadError] = useState<string | null>(null);
  const [quickAddGarmentError, setQuickAddGarmentError] = useState<string | null>(null);
  const [sequentialFlowEnabled, setSequentialFlowEnabled] = useState(false);
  const [activeOutfit, setActiveOutfit] = useState<Outfit | null>(null);

  const saveMutation = useMutation({
    mutationFn: createOutfit,
    onSuccess: (outfit) => {
      setActiveOutfit(outfit);
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  });
  const tryOnMutation = useMutation({
    mutationFn: startTryOn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  });
  const tryOnJobQuery = useQuery({
    queryKey: ['try-on-job', tryOnMutation.data?.id],
    queryFn: () => getTryOnJob(tryOnMutation.data?.id ?? ''),
    enabled: Boolean(tryOnMutation.data?.id)
  });
  const bodyPhotoUploadMutation = useMutation({
    mutationFn: async (file: File) => {
      validateUploadImageFile(file);

      const uploaded = await uploadBodyReferencePhoto(file);
      return await createBodyReferencePhoto(uploaded.url);
    },
    onSuccess: (photo) => {
      setSelectedBodyPhotoId(photo.id);
      setBodyPhotoUploadError(null);
      void queryClient.invalidateQueries({ queryKey: ['body-reference-photos'] });
    },
    onError: (error) => setBodyPhotoUploadError((error as Error).message)
  });
  const deleteBodyPhotoMutation = useMutation({
    mutationFn: deleteBodyReferencePhoto,
    onSuccess: (_, deletedPhotoId) => {
      if (selectedBodyPhotoId === deletedPhotoId) {
        setSelectedBodyPhotoId('');
      }

      void queryClient.invalidateQueries({ queryKey: ['body-reference-photos'] });
    }
  });
  const quickAddGarmentMutation = useMutation({
    mutationFn: async (input: { category: GarmentCategory; file: File }) => {
      validateUploadImageFile(input.file);

      const uploadedPhoto = await uploadGarmentPhoto(input.file);
      return await createGarment({
        name: garmentNameFromFile(input.file, input.category),
        category: input.category,
        imageUrl: uploadedPhoto.url,
        thumbnailUrl: uploadedPhoto.url,
        tags: []
      });
    },
    onSuccess: (garment) => {
      setSelection((current) => garment.category === 'Top'
        ? { ...current, topId: garment.id }
        : { ...current, bottomId: garment.id });
      setQuickAddGarmentError(null);
      void queryClient.invalidateQueries({ queryKey: ['garments'] });
    },
    onError: (error) => setQuickAddGarmentError((error as Error).message)
  });
  const shareMutation = useMutation({ mutationFn: shareOutfit });
  const selectedIds = selectedGarmentIds(selection);
  const selectedGarments = garments.filter((garment) => selectedIds.includes(garment.id));
  const latestTryOnJob = tryOnJobQuery.data ?? tryOnMutation.data;
  const previewUrl = latestTryOnJob?.outputImageUrl ?? activeOutfit?.personPreviewUrl;
  const bodyPhotos = bodyPhotosQuery.data ?? [];
  const selectedBodyPhoto = bodyPhotos.find((photo) => photo.id === selectedBodyPhotoId) ?? bodyPhotos[0];

  useEffect(() => {
    if (!selectedBodyPhotoId && bodyPhotos.length > 0) {
      setSelectedBodyPhotoId(bodyPhotos[0].id);
      return;
    }

    if (selectedBodyPhotoId && bodyPhotos.length > 0 && !bodyPhotos.some((photo) => photo.id === selectedBodyPhotoId)) {
      setSelectedBodyPhotoId(bodyPhotos[0].id);
      return;
    }

    if (selectedBodyPhotoId && bodyPhotos.length === 0) {
      setSelectedBodyPhotoId('');
    }
  }, [bodyPhotos, selectedBodyPhotoId]);

  async function ensureOutfit() {
    if (activeOutfit) {
      return activeOutfit;
    }

    return await saveMutation.mutateAsync({ name: outfitName, garmentIds: selectedIds });
  }

  function handleBodyPhotoFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      bodyPhotoUploadMutation.mutate(file);
    }
  }

  function handleQuickAddGarment(category: GarmentCategory, event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      quickAddGarmentMutation.mutate({ category, file });
    }
  }

  return (
    <section className="builder-layout">
      <aside className="inventory-panel">
        <PanelTitle icon={<Layers3 size={19} />} title="Wardrobe clay" />
        {garmentsQuery.isLoading ? (
          <PanelSkeleton />
        ) : (
          <>
            <SlotPicker
              title="Top"
              category="Top"
              garments={grouped.Top}
              selectedId={selection.topId}
              onSelect={(id) => setSelection({ ...selection, topId: id })}
              onQuickAdd={(event) => handleQuickAddGarment('Top', event)}
              isQuickAdding={quickAddGarmentMutation.isPending && quickAddGarmentMutation.variables?.category === 'Top'}
            />
            <SlotPicker
              title="Bottom"
              category="Bottom"
              garments={grouped.Bottom}
              selectedId={selection.bottomId}
              onSelect={(id) => setSelection({ ...selection, bottomId: id })}
              onQuickAdd={(event) => handleQuickAddGarment('Bottom', event)}
              isQuickAdding={quickAddGarmentMutation.isPending && quickAddGarmentMutation.variables?.category === 'Bottom'}
            />
          </>
        )}
      </aside>

      <div className="preview-stage">
        <header className="builder-header">
          <div>
            <p>Builder</p>
            <h1 style={headingStyle}>{selectionLabel(selection, garments)}</h1>
          </div>
          <ModeToggle mode={mode} onChange={setMode} />
        </header>

        <div className="preview-canvas">
          <div className="preview-topography" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          {mode === 'clothes' ? (
            <div className="clothes-stack">
              {selectedGarments.length === 0 ? <EmptyPreview /> : null}
              {selectedGarments.map((garment, index) => (
                <img
                  key={garment.id}
                  src={garment.thumbnailUrl}
                  alt={garment.name}
                  className={index === 0 ? 'tilt-left' : 'tilt-right'}
                />
              ))}
            </div>
          ) : (
            <div className="person-preview">
              {previewUrl ? <img src={previewUrl} alt="Generated try-on preview" /> : <EmptyPreview />}
            </div>
          )}
        </div>
      </div>

      <aside className="tool-panel builder-controls">
        <PanelTitle icon={<Wand2 size={19} />} title="Outfit controls" />
        <div className="stack">
          <label>
            <span>Outfit name</span>
            <input value={outfitName} onChange={(event) => setOutfitName(event.target.value)} />
          </label>
          <button
            type="button"
            className="clay-button primary-action"
            disabled={selectedIds.length === 0 || saveMutation.isPending}
            onClick={() => saveMutation.mutate({ name: outfitName, garmentIds: selectedIds })}
          >
            <Plus size={16} />
            {saveMutation.isPending ? 'Saving' : 'Save outfit'}
          </button>
          <BodyReferenceManager
            photos={bodyPhotos}
            selectedPhoto={selectedBodyPhoto}
            isLoading={bodyPhotosQuery.isLoading}
            deletingId={deleteBodyPhotoMutation.isPending ? deleteBodyPhotoMutation.variables : undefined}
            onSelect={setSelectedBodyPhotoId}
            onDelete={(id) => deleteBodyPhotoMutation.mutate(id)}
            onUpload={handleBodyPhotoFileChange}
          />
          <button
            type="button"
            className={sequentialFlowEnabled ? 'flow-toggle active' : 'flow-toggle'}
            aria-pressed={sequentialFlowEnabled}
            onClick={() => setSequentialFlowEnabled((enabled) => !enabled)}
          >
            <GitBranch size={16} />
            <span>Sequential flow</span>
            <strong style={headingStyle}>{sequentialFlowEnabled ? 'On' : 'Off'}</strong>
          </button>
          <button
            type="button"
            className="clay-button primary-action generate-action"
            disabled={selectedIds.length === 0 || !selectedBodyPhoto?.imageUrl || tryOnMutation.isPending}
            onClick={async () => {
              const outfit = await ensureOutfit();
              await tryOnMutation.mutateAsync({
                outfitId: outfit.id,
                bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl,
                consentAccepted: true,
                sequentialFlowEnabled
              });
              setMode('person');
            }}
          >
            <Sparkles size={16} />
            {tryOnMutation.isPending ? 'Generating' : 'Generate preview'}
          </button>
          {latestTryOnJob ? (
            <div className="tryon-status">
              <ScanFace size={17} />
              <div>
                <small style={headingStyle}>Try-on job</small>
                <strong style={headingStyle}>{latestTryOnJob.status}</strong>
              </div>
            </div>
          ) : null}
          <button
            type="button"
            className="clay-button secondary-action"
            disabled={!activeOutfit || shareMutation.isPending}
            onClick={() => activeOutfit && shareMutation.mutate(activeOutfit.id)}
          >
            <Link2 size={16} />
            Share
          </button>
          {shareMutation.data ? (
            <Link className="share-link" to={shareMutation.data.url}>
              {shareMutation.data.url}
            </Link>
          ) : null}
          {[quickAddGarmentError ? new Error(quickAddGarmentError) : null, bodyPhotoUploadError ? new Error(bodyPhotoUploadError) : null, saveMutation.error, tryOnMutation.error, shareMutation.error, deleteBodyPhotoMutation.error, tryOnJobQuery.error].filter(Boolean).map((error) => (
            <p className="error" key={(error as Error).message}>
              {(error as Error).message}
            </p>
          ))}
          <OutfitList outfits={outfitsQuery.data ?? []} onPick={setActiveOutfit} />
        </div>
      </aside>
    </section>
  );
}

function CalendarPage() {
  const queryClient = useQueryClient();
  const [visibleMonth, setVisibleMonth] = useState(() => new Date());
  const calendarDays = useMemo(() => buildMonthCalendar(visibleMonth), [visibleMonth]);
  const from = calendarDays[0].isoDate;
  const to = calendarDays[calendarDays.length - 1].isoDate;
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const scheduleQuery = useQuery({ queryKey: ['schedule', from, to], queryFn: () => listSchedule(from, to) });
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [outfitId, setOutfitId] = useState('');
  const mutation = useMutation({
    mutationFn: scheduleOutfit,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['schedule'] })
  });
  const outfits = outfitsQuery.data ?? [];

  return (
    <section className="page-grid calendar-view">
      <div className="workspace">
        <header className="page-header calendar-header">
          <div>
            <p>Calendar</p>
            <h1 style={headingStyle}>{format(visibleMonth, 'MMMM yyyy')}</h1>
          </div>
          <div className="calendar-nav">
            <button type="button" aria-label="Previous month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
              <ChevronLeft size={17} />
            </button>
            <button type="button" aria-label="Next month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
              <ChevronRight size={17} />
            </button>
          </div>
        </header>
        <div className="month-calendar" aria-label="Monthly outfit calendar">
          {weekDayLabels.map((dayLabel) => (
            <div className="weekday-cell" key={dayLabel} style={headingStyle}>
              {dayLabel}
            </div>
          ))}
          {calendarDays.map((day) => {
            const scheduled = scheduleQuery.data?.find((item) => item.date === day.isoDate);
            const outfit = outfits.find((item) => item.id === scheduled?.outfitId);
            return (
              <button
                type="button"
                className={[
                  'calendar-day',
                  day.isCurrentMonth ? '' : 'muted-day',
                  day.isoDate === date ? 'selected-day' : '',
                  isToday(day.date) ? 'today' : ''
                ].filter(Boolean).join(' ')}
                key={day.isoDate}
                onClick={() => setDate(day.isoDate)}
              >
                <span style={headingStyle}>{day.dayNumber}</span>
                {outfit ? <strong style={headingStyle}>{outfit.name}</strong> : <p>No outfit</p>}
              </button>
            );
          })}
        </div>
      </div>
      <aside className="tool-panel">
        <PanelTitle icon={<CalendarDays size={19} />} title="Plan day" />
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate({ date, outfitId });
          }}
        >
          <label>
            <span>Date</span>
            <ClayDatePicker value={date} onChange={setDate} />
          </label>
          <div className="field-block">
            <span className="field-label">Outfit</span>
            <OutfitChoiceList outfits={outfits} selectedId={outfitId} onSelect={setOutfitId} />
          </div>
          <button type="submit" className="clay-button primary-action" disabled={!outfitId || mutation.isPending}>
            <CalendarDays size={16} />
            {mutation.isPending ? 'Planning' : 'Plan day'}
          </button>
        </form>
      </aside>
    </section>
  );
}

function SharePage() {
  const { token } = useParams();
  const query = useQuery({
    queryKey: ['share', token],
    queryFn: () => getSharedOutfit(token ?? ''),
    enabled: Boolean(token)
  });

  if (query.isLoading) {
    return <p className="status">Loading shared outfit...</p>;
  }

  if (!query.data) {
    return <p className="status">Shared outfit not found.</p>;
  }

  return (
    <section className="shared-view">
      <PageHeader
        eyebrow="Shared outfit"
        title={query.data.name}
        text="A tactile snapshot from Outfit Planner, ready to preview without opening the private workspace."
      />
      <div className="preview-canvas shared-canvas">
        <div className="person-preview">
          {query.data.personPreviewUrl ?? query.data.clothesOnlyPreviewUrl ? (
            <img src={query.data.personPreviewUrl ?? query.data.clothesOnlyPreviewUrl ?? ''} alt={query.data.name} />
          ) : (
            <EmptyPreview />
          )}
        </div>
      </div>
    </section>
  );
}

function PageHeader({ eyebrow, title, text }: { eyebrow: string; title: string; text: string }) {
  return (
    <header className="page-header">
      <div>
        <p>{eyebrow}</p>
        <h1 style={headingStyle}>{title}</h1>
      </div>
      <span>{text}</span>
    </header>
  );
}

function PanelTitle({ icon, title }: { icon: ReactNode; title: string }) {
  return (
    <div className="panel-title">
      <span>{icon}</span>
      <h2 style={headingStyle}>{title}</h2>
    </div>
  );
}

function ClayDatePicker({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const selectedDate = dateFromIso(value);
  const [isOpen, setIsOpen] = useState(false);
  const [visibleMonth, setVisibleMonth] = useState(selectedDate);
  const days = useMemo(() => buildMonthCalendar(visibleMonth), [visibleMonth]);

  useEffect(() => {
    setVisibleMonth(selectedDate);
  }, [value]);

  return (
    <div className="clay-date-picker">
      <button
        type="button"
        className="date-trigger"
        aria-label={`Choose date ${format(selectedDate, 'dd.MM.yyyy')}`}
        aria-expanded={isOpen}
        onClick={() => setIsOpen((open) => !open)}
      >
        <span style={headingStyle}>{format(selectedDate, 'dd.MM.yyyy')}</span>
        <CalendarDays size={18} />
      </button>
      {isOpen ? (
        <div className="date-popover" role="dialog" aria-label="Date picker">
          <div className="date-popover-header">
            <strong style={headingStyle}>{format(visibleMonth, 'MMMM yyyy')}</strong>
            <div>
              <button type="button" aria-label="Previous picker month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
                <ChevronLeft size={17} />
              </button>
              <button type="button" aria-label="Next picker month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
                <ChevronRight size={17} />
              </button>
            </div>
          </div>
          <div className="date-weekdays">
            {weekDayLabels.map((label) => (
              <span key={label} style={headingStyle}>{label}</span>
            ))}
          </div>
          <div className="date-grid">
            {days.map((day) => (
              <button
                type="button"
                key={day.isoDate}
                className={[
                  'date-day',
                  day.isCurrentMonth ? '' : 'outside-month',
                  day.isoDate === value ? 'selected' : '',
                  isToday(day.date) ? 'today' : ''
                ].filter(Boolean).join(' ')}
                aria-pressed={day.isoDate === value}
                onClick={() => {
                  onChange(day.isoDate);
                  setIsOpen(false);
                }}
              >
                {day.dayNumber}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="date-today-action"
            onClick={() => {
              onChange(format(new Date(), 'yyyy-MM-dd'));
              setIsOpen(false);
            }}
          >
            Today
          </button>
        </div>
      ) : null}
    </div>
  );
}

function dateFromIso(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function CategorySegmentedControl({
  value,
  onChange
}: {
  value: GarmentCategory;
  onChange: (category: GarmentCategory) => void;
}) {
  return (
    <fieldset className="segmented-field">
      <legend>Type</legend>
      <div className="segmented-control" data-value={value.toLowerCase()} role="radiogroup" aria-label="Garment type">
        <span className="toggle-motion-indicator" aria-hidden="true" />
        {(['Top', 'Bottom'] as const).map((category) => (
          <button
            type="button"
            key={category}
            className={value === category ? 'selected' : ''}
            role="radio"
            aria-checked={value === category}
            onPointerDown={() => onChange(category)}
            onClick={() => onChange(category)}
          >
            <GarmentCategoryIcon category={category} size={16} />
            <span>{category}</span>
          </button>
        ))}
      </div>
    </fieldset>
  );
}

function GarmentCategoryIcon({ category, size = 16 }: { category: GarmentCategory; size?: number }) {
  return category === 'Top' ? <Shirt size={size} /> : <BottomsIcon size={size} />;
}

function BottomsIcon({ size = 16 }: { size?: number }) {
  return (
    <svg
      aria-hidden="true"
      fill="none"
      height={size}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      viewBox="0 0 24 24"
      width={size}
    >
      <path d="M8 5h8l1 4H7l1-4Z" />
      <path d="M7 9 5 20h14L17 9" />
      <path d="M10 9 9 20" />
      <path d="M14 9l1 11" />
    </svg>
  );
}

function FilePicker({
  label,
  fileName,
  onChange
}: {
  label: string;
  fileName?: string;
  onChange: (file: File | null) => void;
}) {
  return (
    <label className="file-picker">
      <span>{label}</span>
      <span className="file-picker-control">
        <span className="file-picker-action">
          <ImagePlus size={18} />
          Choose photo
        </span>
        <span className={fileName ? 'file-picker-name selected' : 'file-picker-name'}>
          {fileName ?? 'No file selected'}
        </span>
      </span>
      <input
        type="file"
        accept="image/png,image/jpeg,image/webp"
        onChange={(event) => onChange(event.target.files?.[0] ?? null)}
        required
      />
    </label>
  );
}

function OutfitChoiceList({
  outfits,
  selectedId,
  onSelect
}: {
  outfits: Outfit[];
  selectedId: string;
  onSelect: (outfitId: string) => void;
}) {
  if (outfits.length === 0) {
    return (
      <div className="choice-empty">
        <Shirt size={16} />
        <span>Save an outfit first</span>
      </div>
    );
  }

  return (
    <div className="choice-list" role="radiogroup" aria-label="Outfit">
      {outfits.map((outfit) => (
        <button
          type="button"
          key={outfit.id}
          role="radio"
          aria-checked={selectedId === outfit.id}
          className={selectedId === outfit.id ? 'selected' : ''}
          onClick={() => onSelect(outfit.id)}
        >
          <Shirt size={16} />
          <span>{outfit.name}</span>
          {selectedId === outfit.id ? <Check size={16} /> : null}
        </button>
      ))}
    </div>
  );
}

function MetricOrb({ label, value, tone }: { label: string; value: number; tone: 'violet' | 'blue' | 'pink' }) {
  return (
    <div className={`metric-orb ${tone}`}>
      <strong style={headingStyle}>{value}</strong>
      <span>{label}</span>
    </div>
  );
}

function BodyReferenceManager({
  photos,
  selectedPhoto,
  isLoading,
  deletingId,
  onSelect,
  onDelete,
  onUpload
}: {
  photos: BodyReferencePhoto[];
  selectedPhoto?: BodyReferencePhoto;
  isLoading: boolean;
  deletingId?: string;
  onSelect: (id: string) => void;
  onDelete: (id: string) => void;
  onUpload: (event: ChangeEvent<HTMLInputElement>) => void;
}) {
  return (
    <section className="body-reference-manager" aria-label="Body references">
      <div className="body-reference-header">
        <h3 style={headingStyle}>Body references</h3>
      </div>
      {isLoading ? (
        <div className="body-reference-skeleton" aria-label="Loading body references" />
      ) : photos.length > 0 ? (
        <div className="body-reference-list">
          {photos.map((photo, index) => (
            <div className="body-reference-item" key={photo.id}>
              <button
                type="button"
                className={photo.id === selectedPhoto?.id ? 'body-reference-option selected' : 'body-reference-option'}
                onClick={() => onSelect(photo.id)}
                aria-pressed={photo.id === selectedPhoto?.id}
              >
                <img src={photo.imageUrl} alt="" />
                <span>{photo.id === selectedPhoto?.id ? 'Selected' : 'Reference'}</span>
              </button>
              <button
                type="button"
                className="icon-action delete-action body-reference-delete"
                aria-label={`Delete body reference ${index + 1}`}
                disabled={deletingId === photo.id}
                onClick={() => onDelete(photo.id)}
              >
                <Trash2 size={15} />
              </button>
            </div>
          ))}
          <label className="body-reference-empty body-reference-upload-tile">
            <Camera size={18} />
            <span>Add body photo</span>
            <input type="file" accept="image/png,image/jpeg,image/webp" onChange={onUpload} />
          </label>
        </div>
      ) : (
        <label className="body-reference-empty">
          <Camera size={18} />
          <span>Add body photo</span>
          <input type="file" accept="image/png,image/jpeg,image/webp" onChange={onUpload} />
        </label>
      )}
    </section>
  );
}

function SlotPicker({
  title,
  category,
  garments,
  selectedId,
  onSelect,
  onQuickAdd,
  isQuickAdding
}: {
  title: string;
  category: GarmentCategory;
  garments: GarmentItem[];
  selectedId?: string;
  onSelect: (id: string) => void;
  onQuickAdd: (event: ChangeEvent<HTMLInputElement>) => void;
  isQuickAdding: boolean;
}) {
  const lowerTitle = title.toLowerCase();

  return (
    <div className="slot-picker">
      <h3 style={headingStyle}>{title}</h3>
      {garments.map((garment) => (
        <button
          type="button"
          key={garment.id}
          className={selectedId === garment.id ? 'garment-button selected' : 'garment-button'}
          onClick={() => onSelect(garment.id)}
        >
          <img src={garment.thumbnailUrl} alt="" />
          <span>{garment.name}</span>
        </button>
      ))}
      {garments.length === 0 ? (
        <label className="inline-empty" aria-disabled={isQuickAdding}>
          <GarmentCategoryIcon category={category} size={18} />
          <span>{isQuickAdding ? `Adding ${lowerTitle}` : `Add a ${lowerTitle} in Wardrobe`}</span>
          <input type="file" accept="image/png,image/jpeg,image/webp" disabled={isQuickAdding} onChange={onQuickAdd} data-category={category} />
        </label>
      ) : null}
    </div>
  );
}

function garmentNameFromFile(file: File, category: GarmentCategory) {
  const name = file.name
    .replace(/\.[^.]+$/, '')
    .replace(/[-_]+/g, ' ')
    .trim();

  return name || category;
}

function GarmentColumn({
  title,
  items,
  deletingId,
  onDelete
}: {
  title: string;
  items: GarmentItem[];
  deletingId?: string;
  onDelete: (id: string) => void;
}) {
  return (
    <section className="garment-column">
      <h2 style={headingStyle}>{title}</h2>
      <div className="garment-grid">
        {items.map((item) => (
          <article className="garment-card" key={item.id}>
            <div className="garment-card-media">
              <img src={item.thumbnailUrl} alt={item.name} />
              <button
                type="button"
                className="icon-action delete-action garment-delete"
                aria-label={`Delete ${item.name}`}
                disabled={deletingId === item.id}
                onClick={() => onDelete(item.id)}
              >
                <Trash2 size={15} />
              </button>
            </div>
            <div>
              <h3 style={headingStyle}>{item.name}</h3>
              <p>{item.bodyZone}</p>
            </div>
          </article>
        ))}
        {items.length === 0 ? <EmptyState title={`No ${title.toLowerCase()} yet`} text="Upload a garment photo to start building outfits." /> : null}
      </div>
    </section>
  );
}

function OutfitList({ outfits, onPick }: { outfits: Outfit[]; onPick: (outfit: Outfit) => void }) {
  if (outfits.length === 0) {
    return null;
  }

  return (
    <div className="saved-list">
      <h3 style={headingStyle}>Saved outfits</h3>
      {outfits.map((outfit) => (
        <button type="button" key={outfit.id} onClick={() => onPick(outfit)}>
          <Eye size={15} />
          {outfit.name}
        </button>
      ))}
    </div>
  );
}

function EmptyPreview() {
  return (
    <div className="empty-preview">
      <span className="empty-preview-orb">
        <Shirt size={42} />
      </span>
      <strong style={headingStyle}>Select garments</strong>
      <span>Preview the outfit as soft digital clay.</span>
    </div>
  );
}

function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="empty-state">
      <Heart size={22} />
      <strong style={headingStyle}>{title}</strong>
      <p>{text}</p>
    </div>
  );
}

function SkeletonGrid() {
  return (
    <div className="skeleton-grid" aria-label="Loading wardrobe">
      {Array.from({ length: 6 }, (_, index) => (
        <div className="skeleton-card" key={index}>
          <span />
          <strong />
          <p />
        </div>
      ))}
    </div>
  );
}

function PanelSkeleton() {
  return (
    <div className="panel-skeleton" aria-label="Loading wardrobe panel">
      {Array.from({ length: 5 }, (_, index) => (
        <span key={index} />
      ))}
    </div>
  );
}

export default App;
