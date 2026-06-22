import { Shirt } from 'lucide-react';

export function EmptyPreview() {
  return (
    <div className="empty-preview">
      <span className="empty-preview-orb">
        <Shirt size={42} />
      </span>
      <strong>Select garments</strong>
      <span>Preview the pieces together before saving the outfit.</span>
    </div>
  );
}
