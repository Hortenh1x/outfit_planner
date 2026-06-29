export function SkeletonGrid() {
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

export function PanelSkeleton() {
  return (
    <div className="panel-skeleton" aria-label="Loading wardrobe panel">
      {Array.from({ length: 5 }, (_, index) => (
        <span key={index} />
      ))}
    </div>
  );
}
