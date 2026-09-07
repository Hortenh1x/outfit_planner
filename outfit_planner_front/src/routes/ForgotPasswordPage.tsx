import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { KeyRound, Send } from 'lucide-react';
import { getPasswordResetAvailability, requestPasswordReset } from '../api/client';
import { PanelTitle } from '../shared/ui/PanelTitle';

const contactEmail = 'dmytro.bolibok@gmail.com';

// Public /forgot-password: asks for the account email and has the API send a one-time reset
// link. When the server has no email delivery configured it says so instead of pretending.
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const availabilityQuery = useQuery({ queryKey: ['password-reset-availability'], queryFn: getPasswordResetAvailability, retry: 1 });
  const requestMutation = useMutation({ mutationFn: () => requestPasswordReset(email) });
  const unavailable = availabilityQuery.data?.available === false;

  return (
    <section className="auth-page">
      <div className="auth-card">
        <PanelTitle icon={<KeyRound size={19} />} title="Reset password" />
        {unavailable ? (
          <p className="auth-terms-notice" role="status">
            Password reset by email is not enabled on this demo server. Write to{' '}
            <a href={`mailto:${contactEmail}`}>{contactEmail}</a> from your account email and the password will be
            reset for you.
          </p>
        ) : requestMutation.isSuccess ? (
          <p className="auth-terms-notice" role="status">
            If an account exists for {email.trim()}, a reset link is on its way. It works once and expires in an hour.
            Check the spam folder if nothing arrives.
          </p>
        ) : (
          <form
            className="stack"
            onSubmit={(event) => {
              event.preventDefault();
              requestMutation.mutate();
            }}
          >
            <p className="auth-terms-notice">Enter the email of your account and we will send you a link to choose a new password.</p>
            <label>
              <span>Email</span>
              <input
                type="email"
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </label>
            <button type="submit" className="primary-action" disabled={requestMutation.isPending || availabilityQuery.isPending}>
              <Send size={16} />
              {requestMutation.isPending ? 'Sending' : 'Send reset link'}
            </button>
            {requestMutation.error ? <p className="error">{requestMutation.error.message}</p> : null}
          </form>
        )}

        <Link className="auth-switch-link" to="/signin">
          Back to sign in
        </Link>
      </div>
    </section>
  );
}
