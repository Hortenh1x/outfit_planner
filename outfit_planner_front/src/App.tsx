import { type ChangeEvent, useEffect, useMemo, useState } from 'react';
import { Link, NavLink, Route, Routes, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addMonths, format, isToday, subMonths } from 'date-fns';
import { CalendarDays, Camera, ChevronLeft, ChevronRight, Eye, GitBranch, ImagePlus, Link2, Plus, Shirt, Sparkles, Trash2, Upload, Wand2 } from 'lucide-react';
import {
  createBodyReferencePhoto,
  createGarment,
  createOutfit,
  deleteBodyReferencePhoto,
  deleteGarment,
  getSharedOutfit,
  listBodyReferencePhotos,
  listGarments,
  listOutfits,
  listSchedule,
  scheduleOutfit,
  shareOutfit,
  startTryOn,
  uploadBodyReferencePhoto,
  uploadGarmentPhoto
} from './api/client';
import { ModeToggle } from './components/ModeToggle';
import { ThemeToggle, type ThemeMode } from './components/ThemeToggle';
import { buildMonthCalendar, weekDayLabels } from './features/calendar/calendarUtils';
import { groupGarmentsByCategory, selectedGarmentIds, selectionLabel } from './features/outfits/outfitUtils';
import { validateUploadImageFile } from './features/uploads/imageFile';
import type { GarmentCategory, GarmentItem, Outfit, OutfitSelection, PreviewMode } from './types';

function App() {
  const [theme, setTheme] = useState<ThemeMode>(() => {
    const storedTheme = localStorage.getItem('outfit-planner-theme');
    return storedTheme === 'dark' ? 'dark' : 'light';
  });

  useEffect(() => {
    localStorage.setItem('outfit-planner-theme', theme);
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <div className="app-shell" data-theme={theme}>
      <aside className="sidebar">
        <Link to="/builder" className="brand">
          <Shirt size={26} />
          <span>Outfit Planner</span>
        </Link>
        <nav>
          <NavLink to="/wardrobe">
            <Upload size={18} />
            Wardrobe
          </NavLink>
          <NavLink to="/builder">
            <Wand2 size={18} />
            Builder
          </NavLink>
          <NavLink to="/calendar">
            <CalendarDays size={18} />
            Calendar
          </NavLink>
        </nav>
        <ThemeToggle theme={theme} onChange={setTheme} />
      </aside>
      <main className="main-panel">
        <Routes>
          <Route path="/" element={<BuilderPage />} />
          <Route path="/wardrobe" element={<WardrobePage />} />
          <Route path="/builder" element={<BuilderPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="/share/:token" element={<SharePage />} />
        </Routes>
      </main>
    </div>
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
    <section className="page-grid">
      <div className="workspace">
        <header className="page-header">
          <p>Wardrobe</p>
          <h1>Catalog your top and bottom pieces</h1>
        </header>
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
      <aside className="tool-panel">
        <h2>Add garment</h2>
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
            Name
            <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
          </label>
          <label>
            Type
            <select
              value={form.category}
              onChange={(event) => setForm({ ...form, category: event.target.value as GarmentCategory })}
            >
              <option value="Top">Top</option>
              <option value="Bottom">Bottom</option>
            </select>
          </label>
          <label>
            Garment photo
            <input
              key={fileInputKey}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              onChange={(event) => {
                const file = event.target.files?.[0];
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

                setPhotoFile(file);
                setPhotoPreviewUrl(URL.createObjectURL(file));
                setUploadError(null);
              }}
              required
            />
          </label>
          {photoPreviewUrl ? (
            <div className="upload-preview">
              <img src={photoPreviewUrl} alt={`${form.name || 'Garment'} upload preview`} />
              <span>{photoFile?.name}</span>
            </div>
          ) : (
            <div className="upload-drop-hint">
              <ImagePlus size={20} />
              <span>Choose a JPG, PNG, or WebP photo from your device.</span>
            </div>
          )}
          <label>
            Tags
            <input value={form.tags} onChange={(event) => setForm({ ...form, tags: event.target.value })} />
          </label>
          <button type="submit" className="primary-action" disabled={createMutation.isPending || !photoFile}>
            <Plus size={16} />
            {createMutation.isPending ? 'Uploading' : 'Add'}
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
  const [consentAccepted, setConsentAccepted] = useState(false);
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
  const previewUrl = tryOnMutation.data?.outputImageUrl ?? activeOutfit?.personPreviewUrl;
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
        <h2>Wardrobe</h2>
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
            <h1>{selectionLabel(selection, garments)}</h1>
          </div>
          <ModeToggle mode={mode} onChange={setMode} />
        </header>

        <div className="preview-canvas">
          {mode === 'clothes' ? (
            <div className="clothes-stack">
              {selectedGarments.length === 0 ? <EmptyPreview /> : null}
              {selectedGarments.map((garment) => (
                <img key={garment.id} src={garment.thumbnailUrl} alt={garment.name} />
              ))}
            </div>
          ) : (
            <div className="person-preview">
              {previewUrl ? <img src={previewUrl} alt="Generated try-on preview" /> : <EmptyPreview />}
            </div>
          )}
        </div>
      </div>

      <aside className="tool-panel">
        <h2>Outfit controls</h2>
        <div className="stack">
          <label>
            Outfit name
            <input value={outfitName} onChange={(event) => setOutfitName(event.target.value)} />
          </label>
          <button
            type="button"
            className="primary-action"
            disabled={selectedIds.length === 0 || saveMutation.isPending}
            onClick={() => saveMutation.mutate({ name: outfitName, garmentIds: selectedIds })}
          >
            <Plus size={16} />
            {saveMutation.isPending ? 'Saving' : 'Save outfit'}
          </button>
          <section className="body-reference-manager" aria-label="Body references">
            <div className="body-reference-header">
              <h3>Body references</h3>
            </div>
            {bodyPhotosQuery.isLoading ? (
              <div className="body-reference-skeleton" aria-label="Loading body references" />
            ) : bodyPhotos.length > 0 ? (
              <div className="body-reference-list">
                {bodyPhotos.map((photo, index) => (
                  <div className="body-reference-item" key={photo.id}>
                    <button
                      type="button"
                      className={photo.id === selectedBodyPhoto?.id ? 'body-reference-option selected' : 'body-reference-option'}
                      onClick={() => setSelectedBodyPhotoId(photo.id)}
                      aria-pressed={photo.id === selectedBodyPhoto?.id}
                    >
                      <img src={photo.imageUrl} alt="" />
                      <span>{photo.id === selectedBodyPhoto?.id ? 'Selected' : 'Reference'}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-action delete-action body-reference-delete"
                      aria-label={`Delete body reference ${index + 1}`}
                      disabled={deleteBodyPhotoMutation.isPending && deleteBodyPhotoMutation.variables === photo.id}
                      onClick={() => deleteBodyPhotoMutation.mutate(photo.id)}
                    >
                      <Trash2 size={15} />
                    </button>
                  </div>
                ))}
                <label className="body-reference-empty body-reference-upload-tile">
                  <Camera size={18} />
                  <span>Add body photo</span>
                  <input type="file" accept="image/png,image/jpeg,image/webp" onChange={handleBodyPhotoFileChange} />
                </label>
              </div>
            ) : (
              <label className="body-reference-empty">
                <Camera size={18} />
                <span>Add body photo</span>
                <input type="file" accept="image/png,image/jpeg,image/webp" onChange={handleBodyPhotoFileChange} />
              </label>
            )}
          </section>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={consentAccepted}
              onChange={(event) => setConsentAccepted(event.target.checked)}
            />
            I consent to AI try-on processing
          </label>
          <button
            type="button"
            className={sequentialFlowEnabled ? 'flow-toggle active' : 'flow-toggle'}
            aria-pressed={sequentialFlowEnabled}
            onClick={() => setSequentialFlowEnabled((enabled) => !enabled)}
          >
            <GitBranch size={16} />
            <span>Sequential flow</span>
            <strong>{sequentialFlowEnabled ? 'On' : 'Off'}</strong>
          </button>
          <button
            type="button"
            className="primary-action"
            disabled={selectedIds.length === 0 || !selectedBodyPhoto?.imageUrl || tryOnMutation.isPending}
            onClick={async () => {
              const outfit = await ensureOutfit();
              await tryOnMutation.mutateAsync({
                outfitId: outfit.id,
                bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl,
                consentAccepted,
                sequentialFlowEnabled
              });
              setMode('person');
            }}
          >
            <Sparkles size={16} />
            {tryOnMutation.isPending ? 'Generating' : 'Generate preview'}
          </button>
          <button
            type="button"
            className="secondary-action"
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
          {[quickAddGarmentError ? new Error(quickAddGarmentError) : null, bodyPhotoUploadError ? new Error(bodyPhotoUploadError) : null, saveMutation.error, tryOnMutation.error, shareMutation.error, deleteBodyPhotoMutation.error].filter(Boolean).map((error) => (
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
    <section className="page-grid">
      <div className="workspace">
        <header className="page-header calendar-header">
          <p>Calendar</p>
          <div className="calendar-title-row">
            <h1>{format(visibleMonth, 'MMMM yyyy')}</h1>
            <div className="calendar-nav">
              <button type="button" aria-label="Previous month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
                <ChevronLeft size={17} />
              </button>
              <button type="button" aria-label="Next month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
                <ChevronRight size={17} />
              </button>
            </div>
          </div>
        </header>
        <div className="month-calendar" aria-label="Monthly outfit calendar">
          {weekDayLabels.map((dayLabel) => (
            <div className="weekday-cell" key={dayLabel}>
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
                <span>{day.dayNumber}</span>
                {outfit ? <strong>{outfit.name}</strong> : <p>No outfit</p>}
              </button>
            );
          })}
        </div>
      </div>
      <aside className="tool-panel">
        <h2>Schedule</h2>
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate({ date, outfitId });
          }}
        >
          <label>
            Date
            <input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          </label>
          <label>
            Outfit
            <select value={outfitId} onChange={(event) => setOutfitId(event.target.value)} required>
              <option value="">Select outfit</option>
              {outfits.map((outfit) => (
                <option key={outfit.id} value={outfit.id}>
                  {outfit.name}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" className="primary-action" disabled={!outfitId || mutation.isPending}>
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
      <header className="page-header">
        <p>Shared outfit</p>
        <h1>{query.data.name}</h1>
      </header>
      <div className="preview-canvas">
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
      <h3>{title}</h3>
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
          <Shirt size={18} />
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
    <section>
      <h2>{title}</h2>
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
              <h3>{item.name}</h3>
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
      <h3>Saved</h3>
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
      <Shirt size={42} />
      <span>Select garments to preview the outfit</span>
    </div>
  );
}

function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="empty-state">
      <Shirt size={22} />
      <strong>{title}</strong>
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
