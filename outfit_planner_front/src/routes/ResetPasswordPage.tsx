import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { KeyRound } from 'lucide-react';
import { confirmPasswordReset } from '../api/client';
import { PanelTitle } from '../shared/ui/PanelTitle';

// Public /reset-password?token=…: the landing page of the emailed link. A successful reset also
// signs the account out everywhere, so the user continues through the regular sign-in.
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [form, setForm] = useState({ password: '', repeatPassword: '' });
  const resetMutation = useMutation({
    mutationFn: () => confirmPasswordReset({ token, password: form.password, repeatPassword: form.repeatPassword })
  });

  return (
    <section className="auth-page">
      <div className="auth-card">
        <PanelTitle icon={<KeyRound size={19} />} title="Choose a new password" />
        {!token ? (
          <p className="auth-terms-notice" role="alert">
            This reset link is incomplete. Open the link from the email again or{' '}
            <Link to="/forgot-password">request a new one</Link>.
          </p>
        ) : resetMutation.isSuccess ? (
          <p className="auth-terms-notice" role="status">
            Your password was changed and every other session was signed out. You can sign in with the new password now.
          </p>
        ) : (
          <form
            className="stack"
            onSubmit={(event) => {
              event.preventDefault();
              resetMutation.mutate();
            }}
          >
            <label>
              <span>New password</span>
              <input
                type="password"
                autoComplete="new-password"
                value={form.password}
                onChange={(event) => setForm({ ...form, password: event.target.value })}
                required
              />
            </label>
            <label>
              <span>Repeat new password</span>
              <input
                type="password"
                autoComplete="new-password"
                value={form.repeatPassword}
                onChange={(event) => setForm({ ...form, repeatPassword: event.target.value })}
                required
              />
            </label>
            <p className="auth-terms-notice">At least 8 characters with a letter and a digit.</p>
            <button type="submit" className="primary-action" disabled={resetMutation.isPending}>
              <KeyRound size={16} />
              {resetMutation.isPending ? 'Saving' : 'Set new password'}
            </button>
            {resetMutation.error ? (
              <p className="error">
                {resetMutation.error.message}{' '}
                <Link to="/forgot-password">Request a new link</Link>
              </p>
            ) : null}
          </form>
        )}

        <Link className="auth-switch-link" to="/signin">
          {resetMutation.isSuccess ? 'Sign in' : 'Back to sign in'}
        </Link>
      </div>
    </section>
  );
}
