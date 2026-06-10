import { LogIn, LogOut, ShieldCheck, UserPlus } from 'lucide-react';
import { NavLink } from 'react-router-dom';
import type { AuthUser } from '../../api/client';

const headingStyle = { fontFamily: 'Nunito, sans-serif' };

export function AuthActions({
  user,
  isSigningOut,
  onLogout
}: {
  user?: AuthUser | null;
  isSigningOut: boolean;
  onLogout: () => void;
}) {
  if (user) {
    return (
      <section className="auth-actions signed-in" aria-label="Account">
        <div className="auth-user-pill">
          <span>
            <ShieldCheck size={17} />
          </span>
          <div>
            <small style={headingStyle}>Signed in</small>
            <strong style={headingStyle}>{user.email ?? user.displayName}</strong>
          </div>
        </div>
        <button type="button" className="auth-nav-action" disabled={isSigningOut} onClick={onLogout}>
          <LogOut size={17} />
          <span>{isSigningOut ? 'Signing out' : 'Sign out'}</span>
        </button>
      </section>
    );
  }

  return (
    <section className="auth-actions" aria-label="Authentication">
      <NavLink to="/signin" className="auth-nav-action">
        <LogIn size={17} />
        <span>Sign in</span>
      </NavLink>
      <NavLink to="/register" className="auth-nav-action register-action">
        <UserPlus size={17} />
        <span>Register</span>
      </NavLink>
    </section>
  );
}
