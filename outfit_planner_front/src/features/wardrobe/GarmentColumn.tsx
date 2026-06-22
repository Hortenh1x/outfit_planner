import { Trash2 } from 'lucide-react';
import { EmptyState } from '../../shared/ui/EmptyState';
import type { GarmentItem } from '../../types';

export function GarmentColumn({
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
