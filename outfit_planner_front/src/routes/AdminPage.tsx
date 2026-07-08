import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins, Download, KeyRound, Search, Sparkles, Trash2, UserRound } from 'lucide-react';
import {
  adjustAdminUserCredits,
  deleteAdminUser,
  getAdminStats,
  getAdminUserExport,
  listAdminUsers,
  purgeAdminUserAiOutputs,
  revokeAdminUserSessions,
  updateAdminUserRole,
  type AdminUser,
  type UserRole
} from '../api/client';
import { useAuthSession } from '../features/auth/authQueries';
import '../features/admin/admin.css';

const PAGE_SIZE = 20;
const ROLE_OPTIONS: UserRole[] = ['Free', 'Premium', 'Admin'];

export const adminUsersQueryKey = ['admin-users'] as const;
export const adminStatsQueryKey = ['admin-stats'] as const;

export function AdminPage() {
  const queryClient = useQueryClient();
  const sessionQuery = useAuthSession();
  const currentUserId = sessionQuery.data?.user.id;

  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState<UserRole | ''>('');
  const [offset, setOffset] = useState(0);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [creditsEditId, setCreditsEditId] = useState<string | null>(null);
  const [creditsDeltaInput, setCreditsDeltaInput] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const statsQuery = useQuery({ queryKey: adminStatsQueryKey, queryFn: getAdminStats });
  const usersQuery = useQuery({
    queryKey: [...adminUsersQueryKey, { search, roleFilter, offset }],
    queryFn: () =>
      listAdminUsers({
        q: search || undefined,
        role: roleFilter || undefined,
        offset,
        limit: PAGE_SIZE
      })
  });

  const refreshAdminData = () => {
    void queryClient.invalidateQueries({ queryKey: adminUsersQueryKey });
    void queryClient.invalidateQueries({ queryKey: adminStatsQueryKey });
  };

  const settleAction = (notice: string) => {
    setActionError(null);
    setActionNotice(notice);
    refreshAdminData();
  };

  const failAction = (error: unknown) => {
    setActionNotice(null);
    setActionError(error instanceof Error ? error.message : 'Admin action failed.');
  };

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: UserRole }) => updateAdminUserRole(userId, role),
    onSuccess: (updated) => settleAction(`${accountLabel(updated)} is now ${updated.role}.`),
    onError: failAction
  });
  const revokeMutation = useMutation({
    mutationFn: (user: AdminUser) => revokeAdminUserSessions(user.id),
    onSuccess: (_, user) => settleAction(`All sessions of ${accountLabel(user)} were revoked.`),
    onError: failAction
  });
  const purgeMutation = useMutation({
    mutationFn: (user: AdminUser) => purgeAdminUserAiOutputs(user.id),
    onSuccess: (result, user) => settleAction(`Purged ${result.purged} AI output(s) of ${accountLabel(user)}.`),
    onError: failAction
  });
  const deleteMutation = useMutation({
    mutationFn: (user: AdminUser) => deleteAdminUser(user.id),
    onSuccess: (_, user) => {
      setConfirmDeleteId(null);
      settleAction(`Account ${accountLabel(user)} was deleted.`);
    },
    onError: failAction
  });
  const creditsMutation = useMutation({
    mutationFn: ({ user, delta }: { user: AdminUser; delta: number }) => adjustAdminUserCredits(user.id, delta),
    onSuccess: (result, { user }) => {
      setCreditsEditId(null);
      setCreditsDeltaInput('');
      settleAction(`AI credits of ${accountLabel(user)}: ${result.balance}.`);
    },
    onError: failAction
  });

  const page = usersQuery.data;
  const users = page?.items ?? [];
  const totalCount = page?.totalCount ?? 0;
  const rangeStart = totalCount === 0 ? 0 : offset + 1;
  const rangeEnd = Math.min(offset + PAGE_SIZE, totalCount);
  const actionPending =
    roleMutation.isPending || revokeMutation.isPending || purgeMutation.isPending || deleteMutation.isPending;

  const handleSearchSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    setOffset(0);
    setSearch(searchInput.trim());
  };

  const handleExport = async (user: AdminUser) => {
    try {
      const data = await getAdminUserExport(user.id);
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `outfit-planner-user-${user.id}.json`;
      anchor.click();
      URL.revokeObjectURL(url);
      settleAction(`Exported the data of ${accountLabel(user)}.`);
    } catch (error) {
      failAction(error);
    }
  };

  return (
    <section className="admin-page" aria-label="Admin panel">
      <header className="admin-hero">
        <div>
          <p>Administration</p>
          <h1>User management</h1>
        </div>
      </header>

      <div className="admin-stats" role="status" aria-label="Application totals">
        <AdminStatTile label="Users" value={statsQuery.data?.totalUsers} />
        <AdminStatTile label="Garments" value={statsQuery.data?.totalGarments} />
        <AdminStatTile label="Outfits" value={statsQuery.data?.totalOutfits} />
        <AdminStatTile label="Try-on jobs" value={statsQuery.data?.totalTryOnJobs} />
      </div>

      <form className="admin-toolbar" onSubmit={handleSearchSubmit}>
        <label className="admin-search">
          <Search size={15} aria-hidden="true" />
          <input
            type="search"
            value={searchInput}
            placeholder="Search by email or username"
            aria-label="Search users"
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </label>
        <label className="admin-role-filter">
          <span>Role</span>
          <select
            value={roleFilter}
            aria-label="Filter by role"
            onChange={(event) => {
              setOffset(0);
              setRoleFilter(event.target.value as UserRole | '');
            }}
          >
            <option value="">All roles</option>
            {ROLE_OPTIONS.map((role) => (
              <option key={role} value={role}>{role}</option>
            ))}
          </select>
        </label>
        <button type="submit" className="secondary-action">Search</button>
      </form>

      {actionError ? <p className="admin-feedback admin-feedback-error" role="alert">{actionError}</p> : null}
      {actionNotice && !actionError ? <p className="admin-feedback" role="status">{actionNotice}</p> : null}

      {usersQuery.isError ? (
        <div className="admin-empty" role="alert">
          <p>Unable to load users{usersQuery.error instanceof Error ? `: ${usersQuery.error.message}` : '.'}</p>
          <button type="button" className="secondary-action" onClick={() => void usersQuery.refetch()}>Retry</button>
        </div>
      ) : usersQuery.isPending ? (
        <div className="panel-skeleton" aria-label="Loading users">
          {Array.from({ length: 5 }, (_, index) => <span key={index} />)}
        </div>
      ) : users.length === 0 ? (
        <div className="admin-empty">
          <p>No users match this filter.</p>
        </div>
      ) : (
        <div className="admin-table-scroll">
          <table className="admin-table">
            <thead>
              <tr>
                <th scope="col">Account</th>
                <th scope="col">Gender</th>
                <th scope="col">Role</th>
                <th scope="col" className="admin-count-column">Garments</th>
                <th scope="col" className="admin-count-column">Outfits</th>
                <th scope="col" className="admin-count-column">Try-ons</th>
                <th scope="col" className="admin-count-column">Sessions</th>
                <th scope="col">Credits</th>
                <th scope="col">Joined</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.id} className={user.id === currentUserId ? 'admin-row-self' : undefined}>
                  <td>
                    <div className="admin-account-cell">
                      <span className="admin-account-avatar">
                        {user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : <UserRound size={15} aria-hidden="true" />}
                      </span>
                      <span className="admin-account-copy">
                        <strong>{accountLabel(user)}</strong>
                        {user.email ? <small>{user.email}</small> : null}
                      </span>
                      {user.id === currentUserId ? <span className="admin-pill">You</span> : null}
                    </div>
                  </td>
                  <td>{user.gender ?? '—'}</td>
                  <td>
                    <div className="admin-role-cell">
                      <select
                        value={user.role}
                        aria-label={`Role of ${accountLabel(user)}`}
                        disabled={user.rolePinned || user.id === currentUserId || actionPending}
                        onChange={(event) => roleMutation.mutate({ userId: user.id, role: event.target.value as UserRole })}
                      >
                        {ROLE_OPTIONS.map((role) => (
                          <option key={role} value={role}>{role}</option>
                        ))}
                      </select>
                      {user.rolePinned ? (
                        <span className="admin-pill admin-pill-pinned" title="This account's role is pinned by email and cannot change.">
                          Pinned
                        </span>
                      ) : null}
                    </div>
                  </td>
                  <td className="admin-count-column">{user.garmentCount}</td>
                  <td className="admin-count-column">{user.outfitCount}</td>
                  <td className="admin-count-column">{user.tryOnJobCount}</td>
                  <td className="admin-count-column">{user.activeSessionCount}</td>
                  <td>
                    {user.creditBalance == null ? (
                      <span className="admin-credits-unlimited" title="Admin accounts have unlimited AI credits.">unlimited</span>
                    ) : creditsEditId === user.id ? (
                      <form
                        className="admin-credits-edit"
                        aria-label={`Adjust AI credits of ${accountLabel(user)}`}
                        onSubmit={(event) => {
                          event.preventDefault();
                          const delta = Number.parseInt(creditsDeltaInput, 10);
                          if (Number.isNaN(delta) || delta === 0) {
                            setActionNotice(null);
                            setActionError('Enter a non-zero credit delta, e.g. 10 or -5.');
                            return;
                          }
                          creditsMutation.mutate({ user, delta });
                        }}
                      >
                        <input
                          type="text"
                          inputMode="numeric"
                          value={creditsDeltaInput}
                          placeholder="+/-"
                          aria-label="Credit delta"
                          onChange={(event) => setCreditsDeltaInput(event.target.value)}
                        />
                        <button type="submit" className="secondary-action" disabled={creditsMutation.isPending}>Apply</button>
                        <button type="button" className="secondary-action" onClick={() => setCreditsEditId(null)}>Cancel</button>
                      </form>
                    ) : (
                      <button
                        type="button"
                        className="admin-credits-value"
                        title="Adjust AI credits"
                        aria-label={`AI credits of ${accountLabel(user)}: ${user.creditBalance}. Adjust`}
                        disabled={actionPending}
                        onClick={() => {
                          setCreditsEditId(user.id);
                          setCreditsDeltaInput('');
                        }}
                      >
                        <Coins size={13} aria-hidden="true" />
                        {user.creditBalance}
                      </button>
                    )}
                  </td>
                  <td>
                    <time dateTime={user.createdAt}>{formatDate(user.createdAt)}</time>
                  </td>
                  <td>
                    {confirmDeleteId === user.id ? (
                      <div className="admin-delete-confirm" role="alertdialog" aria-label={`Confirm deleting ${accountLabel(user)}`}>
                        <span>Delete account and all data?</span>
                        <button
                          type="button"
                          className="primary-action danger-solid"
                          disabled={deleteMutation.isPending}
                          onClick={() => deleteMutation.mutate(user)}
                        >
                          Delete
                        </button>
                        <button type="button" className="secondary-action" onClick={() => setConfirmDeleteId(null)}>
                          Cancel
                        </button>
                      </div>
                    ) : (
                      <div className="admin-row-actions">
                        <button
                          type="button"
                          className="icon-button"
                          title="Revoke all sessions"
                          aria-label={`Revoke all sessions of ${accountLabel(user)}`}
                          disabled={actionPending}
                          onClick={() => revokeMutation.mutate(user)}
                        >
                          <KeyRound size={15} />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          title="Purge AI outputs"
                          aria-label={`Purge AI outputs of ${accountLabel(user)}`}
                          disabled={actionPending}
                          onClick={() => purgeMutation.mutate(user)}
                        >
                          <Sparkles size={15} />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          title="Export data as JSON"
                          aria-label={`Export the data of ${accountLabel(user)}`}
                          disabled={actionPending}
                          onClick={() => void handleExport(user)}
                        >
                          <Download size={15} />
                        </button>
                        <button
                          type="button"
                          className="icon-button admin-delete-button"
                          title={deleteDisabledReason(user, currentUserId) ?? 'Delete account'}
                          aria-label={`Delete the account of ${accountLabel(user)}`}
                          disabled={actionPending || deleteDisabledReason(user, currentUserId) !== null}
                          onClick={() => setConfirmDeleteId(user.id)}
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <footer className="admin-pagination">
        <span>
          {rangeStart}–{rangeEnd} of {totalCount}
        </span>
        <div>
          <button
            type="button"
            className="secondary-action"
            disabled={offset === 0 || usersQuery.isPending}
            onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
          >
            Previous
          </button>
          <button
            type="button"
            className="secondary-action"
            disabled={offset + PAGE_SIZE >= totalCount || usersQuery.isPending}
            onClick={() => setOffset(offset + PAGE_SIZE)}
          >
            Next
          </button>
        </div>
      </footer>
    </section>
  );
}

function AdminStatTile({ label, value }: { label: string; value?: number }) {
  return (
    <article className="admin-stat-tile">
      <strong>{value ?? '—'}</strong>
      <span>{label}</span>
    </article>
  );
}

function accountLabel(user: AdminUser): string {
  return user.username || user.email || user.id;
}

function deleteDisabledReason(user: AdminUser, currentUserId?: string): string | null {
  if (user.rolePinned) {
    return 'Pinned accounts cannot be deleted.';
  }

  if (user.id === currentUserId) {
    return 'Delete your own account from account settings instead.';
  }

  return null;
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}
