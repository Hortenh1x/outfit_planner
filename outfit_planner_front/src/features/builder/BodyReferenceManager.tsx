import type { ChangeEvent } from 'react';
import { Camera, Trash2 } from 'lucide-react';
import type { BodyReferencePhoto } from '../../types';

export function BodyReferenceManager({
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
        <h3>Body references</h3>
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
