import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './AppShell';
import { RequireAdmin } from './RequireAdmin';
import { RequireAuth } from './RequireAuth';
import { AdminPage } from '../routes/AdminPage';
import { AuthPage } from '../routes/AuthPage';
import { BuilderPage } from '../routes/BuilderPage';
import { CalendarPage } from '../routes/CalendarPage';
import { SharePage } from '../routes/SharePage';
import { WardrobePage } from '../routes/WardrobePage';

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/signin" element={<AuthPage mode="signin" />} />
        <Route path="/register" element={<AuthPage mode="register" />} />
        <Route path="/share/:token" element={<SharePage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<Navigate to="/builder" replace />} />
          <Route path="/wardrobe" element={<WardrobePage />} />
          <Route path="/builder" element={<BuilderPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route element={<RequireAdmin />}>
            <Route path="/admin" element={<AdminPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}
