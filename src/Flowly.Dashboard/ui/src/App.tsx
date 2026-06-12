import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { theme } from './lib/theme';
import AppShell from './components/AppShell';
import JobsPage from './pages/JobsPage';
import RecurringJobsPage from './pages/RecurringJobsPage';
import DeadLettersPage from './pages/DeadLettersPage';
import SubmitPage from './pages/SubmitPage';
import { useConfig } from './hooks/useConfig';

function DefaultRedirect() {
  const config = useConfig();
  if (!config) return null;
  if (config.hasJobs) return <Navigate to="/jobs" replace />;
  if (config.hasDeadLetters) return <Navigate to="/dead-letters" replace />;
  if (config.hasSubmitters) return <Navigate to="/submit" replace />;
  return <Navigate to="/jobs" replace />;
}

export default function App() {
  return (
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
  );
}
