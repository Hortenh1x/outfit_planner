import { Navigate, Outlet } from 'react-router-dom';
import { useAuthSession } from '../features/auth/authQueries';

// Admin-only route gate. The parent RequireAuth route already handles the unauthenticated
// and session-error states; anyone signed in without the Admin role is sent home.
export function RequireAdmin() {
  const sessionQuery = useAuthSession();

  if (sessionQuery.isPending) {
    return (
      <div className="panel-skeleton" aria-label="Loading admin page">
        {Array.from({ length: 5 }, (_, index) => <span key={index} />)}
      </div>
    );
  }

  if (sessionQuery.data?.user.role !== 'Admin') {
    return <Navigate to="/builder" replace />;
  }

  return <Outlet />;
}
