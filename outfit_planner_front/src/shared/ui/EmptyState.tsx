import type { CSSProperties } from 'react';
import { Heart } from 'lucide-react';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="empty-state">
      <Heart size={22} />
      <strong style={headingStyle}>{title}</strong>
      <p>{text}</p>
    </div>
  );
}
