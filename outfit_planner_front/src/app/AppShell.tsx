import { type ChangeEvent, type PointerEvent, type RefObject, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Camera, Check, LogOut, ShieldCheck, Shirt, Upload, UserRound, Wand2, X } from 'lucide-react';
import { getAuthProviders, logout, updateAccountProfile, uploadAccountAvatar, type AuthUser, type UserGender } from '../api/client';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import './editorialShell.css';

const themeStorageKey = 'outfit-planner-theme';

function readStoredTheme(): ThemeMode {
  try {
    return localStorage.getItem(themeStorageKey) === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

function persistTheme(theme: ThemeMode): void {
  try {
    localStorage.setItem(themeStorageKey, theme);
  } catch {
    // Ignore storage failures (private mode / disabled storage); the theme still applies this session.
  }
}

export function AppShell() {
  const [theme, setTheme] = useState<ThemeMode>(readStoredTheme);
  const sessionQuery = useAuthSession();
  const authProvidersQuery = useQuery({ queryKey: ['auth-providers'], queryFn: getAuthProviders, retry: 1 });
  const shellRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    persistTheme(theme);
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <div className="editorial-shell" data-theme={theme} ref={shellRef}>
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
        <AccountPanel user={sessionQuery.data?.user} shellRef={shellRef} />
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
  const sessionQuery = useAuthSession();
  const isAdmin = sessionQuery.data?.user.role === 'Admin';

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
      {isAdmin ? (
        <NavLink to="/admin" className={navButtonClass}>
          <ShieldCheck size={18} />
          <span>Admin</span>
        </NavLink>
      ) : null}
    </nav>
  );
}

function AccountPanel({
  user,
  shellRef
}: {
  user?: AuthUser | null;
  shellRef: RefObject<HTMLDivElement | null>;
}) {
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [isAvatarPreviewOpen, setIsAvatarPreviewOpen] = useState(false);
  const [isSignOutConfirmOpen, setIsSignOutConfirmOpen] = useState(false);
  const [username, setUsername] = useState('');
  const [gender, setGender] = useState<UserGender | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const longPressTimerRef = useRef<number | null>(null);
  const longPressTriggeredRef = useRef(false);
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(authSessionQueryKey, null);
      void queryClient.invalidateQueries();
    }
  });
  const profileMutation = useMutation({
    mutationFn: updateAccountProfile,
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
    }
  });
  const avatarMutation = useMutation({
    mutationFn: uploadAccountAvatar,
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
    }
  });

  useEffect(() => {
    if (!user || !isOpen) {
      return;
    }

    setUsername(accountName(user));
    setGender(user.gender ?? null);
  }, [isOpen, user]);

  if (user) {
    const name = accountName(user);
    const avatar = <AccountAvatar user={user} size="small" />;
    const largeAvatar = <AccountAvatar user={user} size="large" />;
    const profileError = profileMutation.error instanceof Error ? profileMutation.error.message : null;
    const avatarError = avatarMutation.error instanceof Error ? avatarMutation.error.message : null;
    const logoutError = logoutMutation.error instanceof Error ? logoutMutation.error.message : null;

    return (
      <section className="editorial-account" aria-label="Account">
        <button type="button" className="editorial-account-user" onClick={() => setIsOpen(true)}>
          {avatar}
          <span className="editorial-account-copy">
            <strong>{name}</strong>
            {user.email && user.email !== name ? <small>{user.email}</small> : null}
          </span>
        </button>
        {isOpen ? createPortal(
          <div className="account-dialog-backdrop" role="presentation" onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setIsOpen(false);
              setIsSignOutConfirmOpen(false);
              setIsAvatarPreviewOpen(false);
            }
          }}>
            <section className="account-dialog" role="dialog" aria-modal="true" aria-label="Account settings">
              <header className="account-dialog-header">
                <div>
                  <small>Account</small>
                  <h2>Settings</h2>
                </div>
                <button type="button" className="icon-button" aria-label="Close account settings" onClick={() => setIsOpen(false)}>
                  <X size={18} />
                </button>
              </header>
              <div className="account-profile-row">
                <button
                  type="button"
                  className="account-avatar-button"
                  aria-label="Open avatar preview"
                  onPointerDown={handleAvatarPointerDown}
                  onPointerUp={handleAvatarPointerUp}
                  onPointerCancel={clearAvatarLongPress}
                  onPointerLeave={clearAvatarLongPress}
                >
                  {largeAvatar}
                  {avatarMutation.isPending ? <span className="account-avatar-busy"><Camera size={16} /></span> : null}
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  hidden
                  onChange={handleAvatarFileChange}
                />
                <div>
                  <strong>{name}</strong>
                  {user.email ? <small>{user.email}</small> : null}
                </div>
              </div>
              <label className="account-field">
                <span>Username</span>
                <input value={username} onChange={(event) => setUsername(event.target.value)} />
              </label>
              <div className="account-field">
                <span>Gender</span>
                <div className="account-segmented-control" role="group" aria-label="Gender">
                  {(['Male', 'Female'] as UserGender[]).map((option) => (
                    <button
                      key={option}
                      type="button"
                      className={gender === option ? 'active' : ''}
                      aria-pressed={gender === option}
                      onClick={() => setGender(option)}
                    >
                      {gender === option ? <Check size={15} /> : null}
                      <span>{option.toLowerCase()}</span>
                    </button>
                  ))}
                </div>
              </div>
              {[profileError, avatarError, logoutError].filter((message): message is string => Boolean(message)).map((message) => (
                <p className="account-error" key={message}>{message}</p>
              ))}
              <div className="account-dialog-actions">
                <button
                  type="button"
                  className="secondary-action danger-action"
                  disabled={logoutMutation.isPending}
                  onClick={() => setIsSignOutConfirmOpen(true)}
                >
                  <LogOut size={16} />
                  {logoutMutation.isPending ? 'Signing out' : 'Sign out'}
                </button>
                <button
                  type="button"
                  className="primary-action"
                  disabled={username.trim().length === 0 || profileMutation.isPending}
                  onClick={() => profileMutation.mutate({ username: username.trim(), gender })}
                >
                  <Check size={16} />
                  {profileMutation.isPending ? 'Saving' : 'Save changes'}
                </button>
              </div>
              {isAvatarPreviewOpen ? (
                <div className="account-avatar-preview" role="dialog" aria-modal="false" aria-label="Avatar preview" onMouseDown={(event) => {
                  if (event.target === event.currentTarget) {
                    setIsAvatarPreviewOpen(false);
                  }
                }}>
                  <button type="button" aria-label="Close avatar preview" onClick={() => setIsAvatarPreviewOpen(false)}>
                    {largeAvatar}
                  </button>
                </div>
              ) : null}
              {isSignOutConfirmOpen ? (
                <div className="account-confirm" role="dialog" aria-modal="true" aria-label="Confirm sign out">
                  <div>
                    <strong>Sign out?</strong>
                    <p>Your current session will be closed on this device.</p>
                  </div>
                  <div>
                    <button type="button" className="secondary-action" onClick={() => setIsSignOutConfirmOpen(false)}>Cancel</button>
                    <button type="button" className="primary-action danger-solid" disabled={logoutMutation.isPending} onClick={() => logoutMutation.mutate()}>
                      Confirm sign out
                    </button>
                  </div>
                </div>
              ) : null}
            </section>
          </div>,
          shellRef.current ?? document.body
        ) : null}
      </section>
    );
  }

  return (
    <section className="editorial-account" aria-label="Authentication">
      <NavLink to="/signin" className={navButtonClass}>
        <span>Sign in</span>
      </NavLink>
      <NavLink to="/register" className={navButtonClass}>
        <span>Register</span>
      </NavLink>
    </section>
  );

  function handleAvatarPointerDown(event: PointerEvent<HTMLButtonElement>) {
    event.currentTarget.setPointerCapture?.(event.pointerId);
    longPressTriggeredRef.current = false;
    clearAvatarLongPress();
    longPressTimerRef.current = window.setTimeout(() => {
      longPressTriggeredRef.current = true;
      fileInputRef.current?.click();
    }, 600);
  }

  function handleAvatarPointerUp() {
    clearAvatarLongPress();
    if (!longPressTriggeredRef.current) {
      setIsAvatarPreviewOpen(true);
    }
  }

  function clearAvatarLongPress() {
    if (longPressTimerRef.current !== null) {
      window.clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
  }

  function handleAvatarFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      avatarMutation.mutate(file);
    }
  }
}

function navButtonClass({ isActive }: { isActive: boolean }) {
  return isActive ? 'editorial-nav-button active' : 'editorial-nav-button';
}

function accountName(user: AuthUser) {
  return user.username || user.displayName || user.email || 'Signed in';
}

function AccountAvatar({ user, size }: { user: AuthUser; size: 'small' | 'large' }) {
  const name = accountName(user);
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');

  return (
    <span className={`account-avatar account-avatar-${size}`}>
      {user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : initials ? <span>{initials}</span> : <UserRound size={size === 'large' ? 34 : 18} />}
    </span>
  );
}
