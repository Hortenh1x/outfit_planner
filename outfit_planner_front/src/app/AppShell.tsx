import { type CSSProperties, useEffect, useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Shirt, Upload, Wand2 } from 'lucide-react';
import { getAuthProviders, logout } from '../api/client';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { AuthActions } from '../features/auth/AuthActions';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import { ClayBlobs } from '../shared/ui/ClayBlobs';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function AppShell() {
  const queryClient = useQueryClient();
  const [theme, setTheme] = useState<ThemeMode>(() => {
    const storedTheme = localStorage.getItem('outfit-planner-theme');
    return storedTheme === 'dark' ? 'dark' : 'light';
  });
  const sessionQuery = useAuthSession();
  const authProvidersQuery = useQuery({ queryKey: ['auth-providers'], queryFn: getAuthProviders, retry: 1 });
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(authSessionQueryKey, null);
      void queryClient.invalidateQueries();
    }
  });

  useEffect(() => {
    localStorage.setItem('outfit-planner-theme', theme);
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <div className="app-shell" data-theme={theme}>
      <ClayBlobs />
      <aside className="sidebar">
        <Link to="/builder" className="brand" style={headingStyle}>
          <span className="brand-orb">
            <Shirt size={26} />
          </span>
          <span>Outfit Planner</span>
        </Link>
        <PrimaryNavigation />
        <AuthActions
          user={sessionQuery.data?.user}
          isSigningOut={logoutMutation.isPending}
          onLogout={() => logoutMutation.mutate()}
        />
        <ThemeToggle theme={theme} onChange={setTheme} />
      </aside>
      <main className="main-panel">
        <Outlet context={{ providers: authProvidersQuery.data ?? [] }} />
      </main>
      <nav className="bottom-navigation" aria-label="Mobile primary navigation">
        <PrimaryNavigation compact />
      </nav>
    </div>
  );
}

function PrimaryNavigation({ compact = false }: { compact?: boolean }) {
  return (
    <nav aria-label={compact ? 'Mobile workspace navigation' : 'Primary navigation'}>
      <NavLink to="/wardrobe">
        <Upload size={18} />
        <span>Wardrobe</span>
      </NavLink>
      <NavLink to="/builder">
        <Wand2 size={18} />
        <span>Builder</span>
      </NavLink>
      <NavLink to="/calendar">
        <CalendarDays size={18} />
        <span>Calendar</span>
      </NavLink>
    </nav>
  );
}
