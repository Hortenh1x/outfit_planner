import { Moon, SunMedium } from 'lucide-react';

export type ThemeMode = 'light' | 'dark';

interface ThemeToggleProps {
  theme: ThemeMode;
  onChange: (theme: ThemeMode) => void;
}

export function ThemeToggle({ theme, onChange }: ThemeToggleProps) {
  const nextTheme = theme === 'light' ? 'dark' : 'light';
  const label = theme === 'light' ? 'Switch to dark theme' : 'Switch to light theme';

  return (
    <button type="button" className="theme-toggle" aria-label={label} onClick={() => onChange(nextTheme)}>
      {theme === 'light' ? <Moon size={17} /> : <SunMedium size={17} />}
      <span>{theme === 'light' ? 'Dark' : 'Light'}</span>
    </button>
  );
}
