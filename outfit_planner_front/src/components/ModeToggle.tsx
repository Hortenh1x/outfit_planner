import { Shirt, UserRound } from 'lucide-react';
import type { PreviewMode } from '../types';

interface ModeToggleProps {
  mode: PreviewMode;
  onChange: (mode: PreviewMode) => void;
}

export function ModeToggle({ mode, onChange }: ModeToggleProps) {
  return (
    <div className="mode-toggle" aria-label="Preview mode">
      <button
        type="button"
        className={mode === 'clothes' ? 'active' : ''}
        aria-pressed={mode === 'clothes'}
        onClick={() => onChange('clothes')}
      >
        <Shirt size={16} />
        Clothes only
      </button>
      <button
        type="button"
        className={mode === 'person' ? 'active' : ''}
        aria-pressed={mode === 'person'}
        onClick={() => onChange('person')}
      >
        <UserRound size={16} />
        On me
      </button>
    </div>
  );
}
