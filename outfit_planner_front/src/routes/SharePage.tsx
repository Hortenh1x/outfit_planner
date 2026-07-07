import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { ApiError, getSharedOutfit } from '../api/client';
import { ComposedOutfitFigure, composedPiecesFromOutfitItems, defaultFigureWidth } from '../features/outfits/ComposedOutfitFigure';
import { EmptyPreview } from '../shared/ui/EmptyPreview';
import { PageHeader } from '../shared/ui/PageHeader';

export function SharePage() {
  const { token } = useParams();
  const query = useQuery({
    queryKey: ['share', token],
    queryFn: () => getSharedOutfit(token ?? ''),
    enabled: Boolean(token),
    retry: false
  });

  const isNotFound = !token || (query.error instanceof ApiError && query.error.status === 404);

  if (isNotFound) {
    return <p className="status">Shared outfit not found.</p>;
  }

  if (query.isError) {
    return (
      <div className="status" role="alert">
        <p>We couldn’t load this shared outfit. Please try again.</p>
        <button type="button" onClick={() => void query.refetch()}>
          Retry
        </button>
      </div>
    );
  }

  if (query.isPending || query.isLoading) {
    return <p className="status">Loading shared outfit...</p>;
  }

  if (!query.data) {
    return <p className="status">Shared outfit not found.</p>;
  }

  const outfit = query.data;
  const pieces = composedPiecesFromOutfitItems(outfit.items);
  const hasComposedPieces = outfit.items.length > 0;

  return (
    <section className="shared-view">
      <PageHeader
        eyebrow="Shared outfit"
        title={outfit.name}
        text="A tactile snapshot from Outfit Planner, ready to preview without opening the private workspace."
      />
      <div className="preview-canvas shared-canvas">
        {hasComposedPieces ? (
          <div className="composed-stage">
            <ComposedOutfitFigure
              gender={outfit.silhouetteGender ?? 'Female'}
              top={pieces.top}
              bottom={pieces.bottom}
              dress={pieces.dress}
              shoes={pieces.shoes}
              outerwear={pieces.outerwear}
              bag={pieces.bag}
              accessories={pieces.accessories}
              width={defaultFigureWidth()}
            />
          </div>
        ) : (
          <EmptyPreview />
        )}
        {outfit.personPreviewUrl ? (
          <div className="person-preview">
            <img src={outfit.personPreviewUrl} alt={`${outfit.name} try-on preview`} />
          </div>
        ) : null}
      </div>
    </section>
  );
}
