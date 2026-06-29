import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { getSharedOutfit } from '../api/client';
import { EmptyPreview } from '../shared/ui/EmptyPreview';
import { PageHeader } from '../shared/ui/PageHeader';

export function SharePage() {
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
