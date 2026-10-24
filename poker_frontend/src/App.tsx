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
import { I18nProvider, useI18n } from './i18n/I18nProvider';
import type { TranslationKey } from './i18n';

function DevLangToggle() {
  const { lang, setLang } = useI18n();
  if (!import.meta.env.DEV) return null;
  return (
    <div className="fixed top-2 right-2 z-50 flex gap-1 bg-white/90 border rounded p-1 shadow">
      {(['en', 'ru'] as const).map((l) => (
        <button
          key={l}
          onClick={() => setLang(l)}
          className={`px-2 py-0.5 text-xs rounded ${lang === l ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
        >
          {l.toUpperCase()}
        </button>
      ))}
    </div>
  );
}

function Nav() {
  const location = useLocation();
  const { t } = useI18n();
  const links: { to: string; label: string; key: TranslationKey }[] = [
    { to: '/', label: '🏆', key: 'nav.leaderboard' },
    { to: '/games', label: '🎮', key: 'nav.games' },
    { to: '/players', label: '👥', key: 'nav.players' },
    { to: '/records', label: '📊', key: 'nav.records' },
    { to: '/animated', label: '⏱', key: 'nav.animated' },
    { to: '/race', label: '📈', key: 'nav.race' },
  ];

  return (
    <nav className="fixed bottom-0 left-0 right-0 bg-white border-t z-50">
      <div className="flex justify-around py-2">
        {links.map(link => (
          <Link
            key={link.to}
            to={link.to}
            title={t(link.key)}
            aria-label={t(link.key)}
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
    <I18nProvider>
    <AuthGate>
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50 pb-16">
        <DevLangToggle />
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
    </I18nProvider>
  );
}
