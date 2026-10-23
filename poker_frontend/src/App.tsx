import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom';
import Leaderboard from './pages/Leaderboard';
import Games from './pages/Games';
import GameDetail from './pages/GameDetail';
import Players from './pages/Players';
import PlayerProfile from './pages/PlayerProfile';
import ChartRace from './pages/ChartRace';
import AnimatedLeaderboard from './pages/AnimatedLeaderboard';
import Records from './pages/Records';
import AuthGate from './api/AuthGate';

function Nav() {
  const location = useLocation();
  const links = [
    { to: '/', label: '🏆' },
    { to: '/games', label: '🎮' },
    { to: '/players', label: '👥' },
    { to: '/records', label: '📊' },
    { to: '/animated', label: '⏱' },
    { to: '/race', label: '📈' },
  ];

  return (
    <nav className="fixed bottom-0 left-0 right-0 bg-white border-t z-50">
      <div className="flex justify-around py-2">
        {links.map(link => (
          <Link
            key={link.to}
            to={link.to}
            className={`text-xl p-2 ${location.pathname === link.to ? 'opacity-100' : 'opacity-40'}`}
          >
            {link.label}
          </Link>
        ))}
      </div>
    </nav>
  );
}

export default function App() {
  return (
    <AuthGate>
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50 pb-16">
        <main className="px-4 pt-4">
          <Routes>
            <Route path="/" element={<Leaderboard />} />
            <Route path="/games" element={<Games />} />
            <Route path="/games/:id" element={<GameDetail />} />
            <Route path="/players" element={<Players />} />
            <Route path="/players/:name" element={<PlayerProfile />} />
            <Route path="/race" element={<ChartRace />} />
            <Route path="/animated" element={<AnimatedLeaderboard />} />
            <Route path="/records" element={<Records />} />
          </Routes>
        </main>
        <Nav />
      </div>
    </BrowserRouter>
    </AuthGate>
  );
}
