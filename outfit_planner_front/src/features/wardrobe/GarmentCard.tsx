import { AlertTriangle, Archive, Copy, Heart, Pencil, Trash2 } from 'lucide-react';
import type { GarmentItem } from '../../types';

interface GarmentCardProps {
  garment: GarmentItem;
  needsBetterPhoto?: boolean;
  pendingAction?: string;
  onArchive: (garment: GarmentItem) => void;
  onDelete: (garment: GarmentItem) => void;
  onDuplicate: (garment: GarmentItem) => void;
  onEdit: (garment: GarmentItem) => void;
  onFavorite: (garment: GarmentItem) => void;
}

export function GarmentCard({
  garment,
  needsBetterPhoto = false,
  pendingAction,
  onArchive,
  onDelete,
  onDuplicate,
  onEdit,
  onFavorite
}: GarmentCardProps) {
  const disabled = Boolean(pendingAction);
  const favoriteLabel = `${garment.isFavorite ? 'Unfavorite' : 'Favorite'} ${garment.name}`;

  return (
    <article
      className={garment.isArchived ? 'wardrobe-card archived' : 'wardrobe-card'}
      aria-busy={disabled}
      data-pending-action={pendingAction}
    >
      <div className="wardrobe-card-image">
        <img src={garment.thumbnailUrl || garment.imageUrl} alt={garment.name} />
        <button
          type="button"
          className={garment.isFavorite ? 'wardrobe-icon-button active' : 'wardrobe-icon-button'}
          aria-label={favoriteLabel}
          disabled={disabled}
          onClick={() => onFavorite(garment)}
        >
          <Heart size={16} fill={garment.isFavorite ? 'currentColor' : 'none'} aria-hidden="true" />
        </button>
      </div>
      <div className="wardrobe-card-body">
        <div>
          <h3>{garment.name}</h3>
          <p>{garment.category}</p>
        </div>
        {garment.tags.length > 0 ? (
          <ul className="wardrobe-card-tags" aria-label={`Tags for ${garment.name}`}>
            {garment.tags.map((tag) => <li key={tag}>{tag}</li>)}
          </ul>
        ) : null}
        {needsBetterPhoto ? (
          <span className="wardrobe-photo-warning" role="status" aria-label={`Needs better photo for ${garment.name}`}>
            <AlertTriangle size={14} aria-hidden="true" />
            Needs better photo?
          </span>
        ) : null}
        <div className="wardrobe-card-actions" aria-label={`Actions for ${garment.name}`}>
          <button type="button" aria-label={`Edit ${garment.name}`} disabled={disabled} onClick={() => onEdit(garment)}>
            <Pencil size={15} aria-hidden="true" />
          </button>
          <button type="button" aria-label={`Duplicate ${garment.name}`} disabled={disabled} onClick={() => onDuplicate(garment)}>
            <Copy size={15} aria-hidden="true" />
          </button>
          <button
            type="button"
            aria-label={`${garment.isArchived ? 'Restore' : 'Archive'} ${garment.name}`}
            disabled={disabled}
            onClick={() => onArchive(garment)}
          >
            <Archive size={15} aria-hidden="true" />
          </button>
          <button type="button" aria-label={`Delete ${garment.name}`} disabled={disabled} onClick={() => onDelete(garment)}>
            <Trash2 size={15} aria-hidden="true" />
          </button>
        </div>
      </div>
    </article>
  );
}
