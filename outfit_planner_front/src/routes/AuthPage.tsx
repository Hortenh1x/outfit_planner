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
  const [form, setForm] = useState({ email: '', password: '', repeatPassword: '', termsAccepted: false });
  const authMutation = useMutation({
    mutationFn: () => mode === 'register'
      ? register({ email: form.email, password: form.password, repeatPassword: form.repeatPassword, termsAccepted: form.termsAccepted })
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
  // Only sign-in providers this server actually has credentials for are offered; a disabled
  // "Apple" button with no explanation is worse than no button.
  const externalProviders = providers.filter(
    (provider): provider is AuthProvider & { id: 'google' | 'apple' } =>
      (provider.id === 'google' || provider.id === 'apple') && provider.configured
  );
  const externalProviderNames = externalProviders.map((provider) => provider.label).join(' or ');

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
          {mode === 'register' ? (
            <label className="auth-terms-checkbox">
              <input
                type="checkbox"
                checked={form.termsAccepted}
                onChange={(event) => setForm({ ...form, termsAccepted: event.target.checked })}
                required
              />
              <span>
                I agree to the <Link to="/terms">Terms of Use</Link> and <Link to="/privacy">Privacy Policy</Link>
              </span>
            </label>
          ) : null}
          <button
            type="submit"
            className="primary-action"
            disabled={authMutation.isPending || (mode === 'register' && !form.termsAccepted)}
          >
            {mode === 'register' ? <UserPlus size={16} /> : <LogIn size={16} />}
            {authMutation.isPending ? 'Working' : title}
          </button>
          {authMutation.error ? <p className="error">{authMutation.error.message}</p> : null}
        </form>

        {mode === 'signin' ? (
          <Link className="auth-switch-link auth-forgot-link" to="/forgot-password">
            Forgot your password?
          </Link>
        ) : null}

        {externalProviders.length > 0 ? (
          <>
            <p className="auth-terms-notice">
              By {mode === 'register' ? `continuing with ${externalProviderNames}` : `signing in with ${externalProviderNames}`} you also agree to the{' '}
              <Link to="/terms">Terms of Use</Link> and <Link to="/privacy">Privacy Policy</Link>.
            </p>

            <div className="external-auth-actions">
              {externalProviders.map((provider) => (
                <button
                  key={provider.id}
                  type="button"
                  className="oauth-button"
                  onClick={() => window.location.assign(buildExternalAuthUrl(provider.id, returnUrl))}
                >
                  <span>{provider.label.charAt(0).toUpperCase()}</span>
                  {provider.label}
                </button>
              ))}
            </div>
          </>
        ) : mode === 'signin' ? (
          <p className="auth-terms-notice">
            By signing in you agree to the <Link to="/terms">Terms of Use</Link> and <Link to="/privacy">Privacy Policy</Link>.
          </p>
        ) : null}

        <Link className="auth-switch-link" to={alternate.to}>
          {alternate.label}
        </Link>
      </div>
    </section>
  );
}
