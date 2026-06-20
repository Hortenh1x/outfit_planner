import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, LogOut, Shirt, Sparkles, Upload, Wand2 } from 'lucide-react';
import { getAuthProviders, logout } from '../api/client';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import './editorialShell.css';

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
    <div className="editorial-shell" data-theme={theme}>
      <aside className="editorial-sidebar">
        <Link to="/builder" className="editorial-brand">
          <span className="editorial-brand-mark" aria-hidden="true">
            <Shirt size={24} />
          </span>
          <span className="editorial-brand-copy">
            <span>Outfit Planner</span>
            <small>Personal wardrobe studio</small>
          </span>
        </Link>
        <PrimaryNavigation />
        <AccountPanel
          user={sessionQuery.data?.user}
          isSigningOut={logoutMutation.isPending}
          onLogout={() => logoutMutation.mutate()}
        />
        <div className="editorial-theme-row">
          <ThemeToggle theme={theme} onChange={setTheme} />
        </div>
      </aside>
      <main className="editorial-main-panel">
        <Outlet context={{ providers: authProvidersQuery.data ?? [] }} />
      </main>
      <PrimaryNavigation compact />
    </div>
  );
}

function PrimaryNavigation({ compact = false }: { compact?: boolean }) {
  return (
    <nav
      className={compact ? 'editorial-bottom-navigation editorial-nav' : 'editorial-nav'}
      aria-label={compact ? 'Mobile primary navigation' : 'Primary navigation'}
    >
      <NavLink to="/wardrobe" className={navButtonClass}>
        <Upload size={18} />
        <span>Wardrobe</span>
      </NavLink>
      <NavLink to="/builder" className={navButtonClass}>
        <Wand2 size={18} />
        <span>Builder</span>
      </NavLink>
      <NavLink to="/calendar" className={navButtonClass}>
        <CalendarDays size={18} />
        <span>Calendar</span>
      </NavLink>
    </nav>
  );
}

function AccountPanel({
  user,
  isSigningOut,
  onLogout
}: {
  user?: { email?: string | null; displayName?: string | null } | null;
  isSigningOut: boolean;
  onLogout: () => void;
}) {
  if (user) {
    const accountName = user.displayName || user.email || 'Signed in';

    return (
      <section className="editorial-account" aria-label="Account">
        <div className="editorial-account-kicker">
          <Sparkles size={15} />
          <span>Studio session</span>
        </div>
        <div className="editorial-account-user">
          <strong>{accountName}</strong>
          {user.email && user.email !== accountName ? <small>{user.email}</small> : null}
        </div>
        <button
          type="button"
          className="editorial-nav-button editorial-account-action"
          disabled={isSigningOut}
          onClick={onLogout}
        >
          <LogOut size={17} />
          <span>{isSigningOut ? 'Signing out' : 'Sign out'}</span>
        </button>
      </section>
    );
  }

  return (
    <section className="editorial-account" aria-label="Authentication">
      <div className="editorial-account-kicker">
        <Sparkles size={15} />
        <span>Studio access</span>
      </div>
      <NavLink to="/signin" className={navButtonClass}>
        <span>Sign in</span>
      </NavLink>
      <NavLink to="/register" className={navButtonClass}>
        <span>Register</span>
      </NavLink>
    </section>
  );
}

function navButtonClass({ isActive }: { isActive: boolean }) {
  return isActive ? 'editorial-nav-button active' : 'editorial-nav-button';
}
