import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ImagePlus, Plus } from 'lucide-react';
import { createGarment, deleteGarment, listGarments, uploadGarmentPhoto } from '../api/client';
import { GARMENT_CATEGORIES, groupGarmentsByCategory } from '../features/outfits/outfitUtils';
import { validateUploadImageFile } from '../features/uploads/imageFile';
import { GarmentColumn } from '../features/wardrobe/GarmentColumn';
import { CategorySegmentedControl } from '../shared/ui/GarmentCategoryControl';
import { FilePicker } from '../shared/ui/FilePicker';
import { MetricOrb } from '../shared/ui/MetricOrb';
import { PageHeader } from '../shared/ui/PageHeader';
import { PanelTitle } from '../shared/ui/PanelTitle';
import { SkeletonGrid } from '../shared/ui/Skeletons';
import type { GarmentCategory } from '../types';

export function WardrobePage() {
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
            {GARMENT_CATEGORIES.map((category) => (
              <GarmentColumn
                key={category}
                title={category}
                items={grouped[category]}
                deletingId={deleteGarmentMutation.isPending ? deleteGarmentMutation.variables : undefined}
                onDelete={(id) => deleteGarmentMutation.mutate(id)}
              />
            ))}
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
