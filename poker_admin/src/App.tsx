import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import GamesList from './pages/GamesList';
import GamePage from './pages/GamePage';
import PlayersPage from './pages/PlayersPage';
import DatabasePage from './pages/DatabasePage';
import AccessPage from './pages/AccessPage';
import AuthGate from './api/AuthGate';

export default function App() {
  // When served under /admin/ (single-app prod), Vite sets BASE_URL to "/admin/".
  // Strip the trailing slash for the router basename ("/admin"); "/" stays undefined.
  const base = import.meta.env.BASE_URL.replace(/\/$/, '') || undefined;
  return (
    <AuthGate>
      <BrowserRouter basename={base}>
        <div className="min-h-screen bg-gray-50">
          <nav className="bg-white border-b p-3 mb-4">
            <div className="max-w-md mx-auto flex gap-4">
              <Link to="/" className="font-medium hover:text-blue-600">Games</Link>
              <Link to="/players" className="font-medium hover:text-blue-600">Players</Link>
              <Link to="/access" className="font-medium hover:text-blue-600">Access</Link>
              <Link to="/database" className="font-medium hover:text-blue-600">Database</Link>
            </div>
          </nav>
          <main className="max-w-md mx-auto px-4">
            <Routes>
              <Route path="/" element={<GamesList />} />
              <Route path="/games/:id" element={<GamePage />} />
              <Route path="/players" element={<PlayersPage />} />
              <Route path="/access" element={<AccessPage />} />
              <Route path="/database" element={<DatabasePage />} />
            </Routes>
          </main>
        </div>
      </BrowserRouter>
    </AuthGate>
  );
}
