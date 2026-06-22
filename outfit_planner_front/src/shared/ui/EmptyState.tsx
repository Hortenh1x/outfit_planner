import { Heart } from 'lucide-react';

export function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="empty-state">
      <Heart size={22} />
      <strong>{title}</strong>
      <p>{text}</p>
    </div>
  );
}
