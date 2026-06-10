import type { CSSProperties } from 'react';
import { Shirt } from 'lucide-react';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function EmptyPreview() {
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
