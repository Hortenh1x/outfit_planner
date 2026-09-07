import { type ChangeEvent, type PointerEvent, type RefObject, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Camera, Check, Download, Info, LogOut, ShieldCheck, Shirt, Sparkles, Trash2, Upload, UserRound, Wand2, X } from 'lucide-react';
import { billingStatusQueryKey, deleteAccount, exportAccount, getAuthProviders, getBillingStatus, listSessions, logout, openBillingPortal, revokeAllSessions, updateAccountProfile, uploadAccountAvatar, type AuthUser, type UserGender } from '../api/client';
import { redirectToCheckout } from '../features/billing/checkoutRedirect';
import { ThemeToggle, type ThemeMode } from '../components/ThemeToggle';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import { DemoNotice } from './DemoNotice';
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
        <DemoNotice />
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
      <NavLink to="/upgrade" className={navButtonClass}>
        <Sparkles size={18} />
        <span>Premium</span>
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
  const [isSignOutEverywhereConfirmOpen, setIsSignOutEverywhereConfirmOpen] = useState(false);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [username, setUsername] = useState('');
  const [gender, setGender] = useState<UserGender | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const longPressTimerRef = useRef<number | null>(null);
  const longPressTriggeredRef = useRef(false);
  // Signing out drops every cached query: the next account must not glimpse this one's wardrobe.
  const forgetSession = () => {
    queryClient.clear();
    queryClient.setQueryData(authSessionQueryKey, null);
  };
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: forgetSession
  });
  const revokeSessionsMutation = useMutation({
    mutationFn: revokeAllSessions,
    onSuccess: forgetSession
  });
  const deleteAccountMutation = useMutation({
    mutationFn: deleteAccount,
    onSuccess: forgetSession
  });
  const profileMutation = useMutation({
    mutationFn: updateAccountProfile,
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
      // Closing is the confirmation: the sidebar card shows the saved name right away.
      setIsOpen(false);
    }
  });
  const avatarMutation = useMutation({
    mutationFn: uploadAccountAvatar,
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
    }
  });
  // Billing surface inside the settings dialog only — fetched when it opens.
  const billingQuery = useQuery({
    queryKey: billingStatusQueryKey,
    queryFn: getBillingStatus,
    enabled: isOpen && Boolean(user),
    retry: 1
  });
  const portalMutation = useMutation({
    mutationFn: openBillingPortal,
    onSuccess: ({ url }) => redirectToCheckout(url)
  });
  const sessionsQuery = useQuery({
    queryKey: ['auth-sessions'],
    queryFn: listSessions,
    enabled: isOpen && Boolean(user),
    retry: 1
  });
  const activeSessionCount = (Array.isArray(sessionsQuery.data) ? sessionsQuery.data : []).filter((session) => !session.revokedAt).length;

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
    const portalError = portalMutation.error instanceof Error ? portalMutation.error.message : null;
    const revokeError = revokeSessionsMutation.error instanceof Error ? revokeSessionsMutation.error.message : null;
    const deleteError = deleteAccountMutation.error instanceof Error ? deleteAccountMutation.error.message : null;

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
              setIsSignOutEverywhereConfirmOpen(false);
              setIsDeleteConfirmOpen(false);
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
              <div className="account-field">
                <span>Plan</span>
                <div className="account-billing-row">
                  <strong>{user.role ?? 'Free'}</strong>
                  {billingQuery.data?.enabled ? (
                    user.role === 'Free' ? (
                      <Link to="/upgrade" className="account-upgrade-link" onClick={() => setIsOpen(false)}>
                        See Premium
                      </Link>
                    ) : billingQuery.data.portalAvailable ? (
                      <button
                        type="button"
                        className="secondary-action"
                        disabled={portalMutation.isPending}
                        onClick={() => portalMutation.mutate()}
                      >
                        {portalMutation.isPending ? 'Opening portal' : 'Manage subscription'}
                      </button>
                    ) : null
                  ) : null}
                </div>
              </div>
              <Link to="/legal" className="secondary-action account-legal-link" onClick={() => setIsOpen(false)}>
                <Info size={16} />
                <span>Info</span>
              </Link>
              <div className="account-field">
                <span>Your data</span>
                <div className="account-data-actions">
                  <button type="button" className="secondary-action" disabled={isExporting} onClick={() => void exportData()}>
                    <Download size={16} />
                    {isExporting ? 'Preparing export' : 'Download my data'}
                  </button>
                  <button
                    type="button"
                    className="secondary-action"
                    disabled={revokeSessionsMutation.isPending}
                    onClick={() => setIsSignOutEverywhereConfirmOpen(true)}
                  >
                    <LogOut size={16} />
                    {activeSessionCount > 1 ? `Sign out everywhere (${activeSessionCount})` : 'Sign out everywhere'}
                  </button>
                  <button
                    type="button"
                    className="secondary-action danger-action"
                    disabled={deleteAccountMutation.isPending}
                    onClick={() => {
                      setDeleteConfirmText('');
                      setIsDeleteConfirmOpen(true);
                    }}
                  >
                    <Trash2 size={16} />
                    Delete account
                  </button>
                </div>
              </div>
              {[profileError, avatarError, logoutError, portalError, exportError, revokeError, deleteError].filter((message): message is string => Boolean(message)).map((message) => (
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
              {isSignOutEverywhereConfirmOpen ? (
                <div className="account-confirm" role="dialog" aria-modal="true" aria-label="Confirm sign out everywhere">
                  <div>
                    <strong>Sign out on every device?</strong>
                    <p>
                      {activeSessionCount > 1
                        ? `All ${activeSessionCount} active sessions, including this one, will be closed.`
                        : 'Every active session, including this one, will be closed.'}
                    </p>
                  </div>
                  <div>
                    <button type="button" className="secondary-action" onClick={() => setIsSignOutEverywhereConfirmOpen(false)}>Cancel</button>
                    <button type="button" className="primary-action danger-solid" disabled={revokeSessionsMutation.isPending} onClick={() => revokeSessionsMutation.mutate()}>
                      {revokeSessionsMutation.isPending ? 'Signing out' : 'Sign out everywhere'}
                    </button>
                  </div>
                </div>
              ) : null}
              {isDeleteConfirmOpen ? (
                <div className="account-confirm" role="dialog" aria-modal="true" aria-label="Confirm account deletion">
                  <div>
                    <strong>Delete your account?</strong>
                    <p>
                      This permanently removes your garments, outfits, calendar, body photos and generated previews.
                      It cannot be undone. Type DELETE to confirm.
                    </p>
                    <input
                      className="account-confirm-input"
                      aria-label="Type DELETE to confirm"
                      autoComplete="off"
                      value={deleteConfirmText}
                      onChange={(event) => setDeleteConfirmText(event.target.value)}
                    />
                  </div>
                  <div>
                    <button type="button" className="secondary-action" onClick={() => setIsDeleteConfirmOpen(false)}>Cancel</button>
                    <button
                      type="button"
                      className="primary-action danger-solid"
                      disabled={deleteConfirmText.trim() !== 'DELETE' || deleteAccountMutation.isPending}
                      onClick={() => deleteAccountMutation.mutate()}
                    >
                      {deleteAccountMutation.isPending ? 'Deleting' : 'Delete account'}
                    </button>
                  </div>
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

  // The same sanitized export the API offers at GET /api/account/export, saved as a JSON file.
  async function exportData() {
    setIsExporting(true);
    setExportError(null);
    try {
      const data = await exportAccount();
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'outfit-planner-account-export.json';
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setExportError(error instanceof Error ? error.message : String(error));
    } finally {
      setIsExporting(false);
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
