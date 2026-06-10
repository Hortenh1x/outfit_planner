import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthSession } from '../features/auth/authQueries';
import { buildReturnUrlParam } from '../features/auth/returnUrl';

export function RequireAuth() {
  const location = useLocation();
  const sessionQuery = useAuthSession();

  if (sessionQuery.isPending) {
    return (
      <div className="panel-skeleton" aria-label="Loading private page">
        {Array.from({ length: 5 }, (_, index) => <span key={index} />)}
      </div>
    );
  }

  if (sessionQuery.isError) {
    return (
      <section className="auth-page" role="alert">
        <div className="auth-card">
          <p>Unable to verify your session. Try again in a moment.</p>
        </div>
      </section>
    );
  }

  if (!sessionQuery.data) {
    const returnUrl = buildReturnUrlParam(location.pathname, location.search);

    return <Navigate to={`/signin?returnUrl=${encodeURIComponent(returnUrl)}`} replace />;
  }

  return <Outlet />;
}
