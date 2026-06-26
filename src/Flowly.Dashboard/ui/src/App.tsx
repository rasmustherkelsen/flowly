import { createContext, useEffect, useMemo, useState } from 'react';
import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { createAppTheme } from './lib/theme';
import AppShell from './components/AppShell';
import JobsPage from './pages/JobsPage';
import RecurringJobsPage from './pages/RecurringJobsPage';
import DeadLettersPage from './pages/DeadLettersPage';
import SubmitPage from './pages/SubmitPage';
import { useConfig } from './hooks/useConfig';

export const ColorModeContext = createContext({ toggleColorMode: () => {} });

function DefaultRedirect() {
  const config = useConfig();
  if (!config) return null;
  if (config.hasJobs) return <Navigate to="/jobs" replace />;
  if (config.hasDeadLetters) return <Navigate to="/dead-letters" replace />;
  if (config.hasSubmitters) return <Navigate to="/submit" replace />;
  return <Navigate to="/jobs" replace />;
}

export default function App() {
  const [mode, setMode] = useState<'light' | 'dark'>(
    () => (localStorage.getItem('flowly-color-mode') as 'light' | 'dark')
      ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
  );

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => {
      if (!localStorage.getItem('flowly-color-mode')) {
        setMode(e.matches ? 'dark' : 'light');
      }
    };
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  const colorMode = useMemo(() => ({
    toggleColorMode: () =>
      setMode(prev => {
        const next = prev === 'light' ? 'dark' : 'light';
        localStorage.setItem('flowly-color-mode', next);
        return next;
      }),
  }), []);

  const theme = useMemo(() => createAppTheme(mode), [mode]);

  return (
    <ColorModeContext.Provider value={colorMode}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <HashRouter>
          <AppShell>
            <Routes>
              <Route path="/" element={<DefaultRedirect />} />
              <Route path="/jobs" element={<JobsPage />} />
              <Route path="/recurring-jobs" element={<RecurringJobsPage />} />
              <Route path="/dead-letters" element={<DeadLettersPage />} />
              <Route path="/submit" element={<SubmitPage />} />
            </Routes>
          </AppShell>
        </HashRouter>
      </ThemeProvider>
    </ColorModeContext.Provider>
  );
}
