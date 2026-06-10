import { useState } from 'react';
import { Link, useNavigate, useOutletContext, useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { LogIn, UserPlus } from 'lucide-react';
import { buildExternalAuthUrl, login, register, type AuthProvider } from '../api/client';
import { authSessionQueryKey } from '../features/auth/authQueries';
import { readSafeReturnUrl } from '../features/auth/returnUrl';
import { PanelTitle } from '../shared/ui/PanelTitle';

export function AuthPage({ mode }: { mode: 'signin' | 'register' }) {
  const { providers } = useOutletContext<{ providers: AuthProvider[] }>();
  const [searchParams] = useSearchParams();
  const returnUrl = readSafeReturnUrl(searchParams.get('returnUrl'));

  return <AuthPageContent mode={mode} providers={providers} returnUrl={returnUrl} />;
}

export function AuthPageContent({
  mode,
  providers,
  returnUrl
}: {
  mode: 'signin' | 'register';
  providers: AuthProvider[];
  returnUrl: string;
}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ email: '', password: '', repeatPassword: '' });
  const authMutation = useMutation({
    mutationFn: () => mode === 'register'
      ? register({ email: form.email, password: form.password, repeatPassword: form.repeatPassword })
      : login({ email: form.email, password: form.password }),
    onSuccess: (session) => {
      queryClient.setQueryData(authSessionQueryKey, session);
      void queryClient.invalidateQueries();
      navigate(returnUrl);
    }
  });
  const title = mode === 'register' ? 'Register' : 'Sign in';
  const alternate = mode === 'register'
    ? { to: '/signin', label: 'Sign in' }
    : { to: '/register', label: 'Register' };
  const googleProvider = providers.find((provider) => provider.id === 'google');
  const appleProvider = providers.find((provider) => provider.id === 'apple');

  return (
    <section className="auth-page">
      <div className="auth-card">
        <PanelTitle icon={mode === 'register' ? <UserPlus size={19} /> : <LogIn size={19} />} title={title} />
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            authMutation.mutate();
          }}
        >
          <label>
            <span>Email</span>
            <input
              type="email"
              autoComplete="email"
              value={form.email}
              onChange={(event) => setForm({ ...form, email: event.target.value })}
              required
            />
          </label>
          <label>
            <span>Password</span>
            <input
              type="password"
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
              minLength={8}
              pattern={mode === 'register' ? '^(?=.*[A-Za-z])(?=.*\\d).{8,}$' : undefined}
              title={mode === 'register' ? 'Use at least 8 characters with at least one letter and one digit.' : undefined}
              value={form.password}
              onChange={(event) => setForm({ ...form, password: event.target.value })}
              required
            />
          </label>
          {mode === 'register' ? (
            <label>
              <span>Repeat password</span>
              <input
                type="password"
                autoComplete="new-password"
                minLength={8}
                pattern="^(?=.*[A-Za-z])(?=.*\d).{8,}$"
                title="Use at least 8 characters with at least one letter and one digit."
                value={form.repeatPassword}
                onChange={(event) => setForm({ ...form, repeatPassword: event.target.value })}
                required
              />
            </label>
          ) : null}
          <button type="submit" className="clay-button primary-action" disabled={authMutation.isPending}>
            {mode === 'register' ? <UserPlus size={16} /> : <LogIn size={16} />}
            {authMutation.isPending ? 'Working' : title}
          </button>
          {authMutation.error ? <p className="error">{authMutation.error.message}</p> : null}
        </form>

        <div className="external-auth-actions">
          <button
            type="button"
            className="oauth-button"
            disabled={!googleProvider?.configured}
            onClick={() => window.location.assign(buildExternalAuthUrl('google', returnUrl))}
          >
            <span>G</span>
            Google
          </button>
          <button
            type="button"
            className="oauth-button"
            disabled={!appleProvider?.configured}
            onClick={() => window.location.assign(buildExternalAuthUrl('apple', returnUrl))}
          >
            <span>A</span>
            Apple
          </button>
        </div>

        <Link className="auth-switch-link" to={alternate.to}>
          {alternate.label}
        </Link>
      </div>
    </section>
  );
}
