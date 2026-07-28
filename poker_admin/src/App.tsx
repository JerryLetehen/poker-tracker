import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import GamesList from './pages/GamesList';
import GamePage from './pages/GamePage';
import PlayersPage from './pages/PlayersPage';

export default function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50">
        <nav className="bg-white border-b p-3 mb-4">
          <div className="max-w-md mx-auto flex gap-4">
            <Link to="/" className="font-medium hover:text-blue-600">Games</Link>
            <Link to="/players" className="font-medium hover:text-blue-600">Players</Link>
          </div>
        </nav>
        <main className="max-w-md mx-auto px-4">
          <Routes>
            <Route path="/" element={<GamesList />} />
            <Route path="/games/:id" element={<GamePage />} />
            <Route path="/players" element={<PlayersPage />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
