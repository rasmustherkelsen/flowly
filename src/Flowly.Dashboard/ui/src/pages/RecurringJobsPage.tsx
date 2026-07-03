import { useCallback, useState } from 'react';
import {
  Alert, Box, Card, CircularProgress, IconButton, Snackbar, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Tooltip, Typography,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import PageHeader from '../components/PageHeader';
import { useAutoRefresh } from '../hooks/useAutoRefresh';
import { useCanSubmit } from '../hooks/useCanSubmit';
import { formatDate } from '../lib/formatters';
import type { RecurringJobInfo } from '../types';

export default function RecurringJobsPage() {
  const canSubmit = useCanSubmit();
  const [triggering, setTriggering] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const fetchFn = useCallback(async () => {
    const res = await fetch('api/recurring-jobs');
    if (!res.ok) {
      if (res.status === 404) return [] as RecurringJobInfo[];
      throw new Error(`HTTP ${res.status}`);
    }
    return res.json() as Promise<RecurringJobInfo[]>;
  }, []);

  const { data, loading, error, refresh } = useAutoRefresh(fetchFn, 5000);

  const runNow = async (jobId: string) => {
    setTriggering(jobId);
    try {
      const res = await fetch(`api/recurring-jobs/${jobId}/trigger`, { method: 'POST' });
      if (!res.ok) {
        setToast(`Failed to trigger job (HTTP ${res.status}).`);
        return;
      }
      await refresh();
    } finally {
      setTriggering(null);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Recurring Jobs"
        loading={loading}
        onRefresh={refresh}
        totalCount={data?.length}
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Card>
        {loading && !data ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}><CircularProgress /></Box>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Description</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Schedule</TableCell>
                  <TableCell>Last Started</TableCell>
                  <TableCell>Last Completed</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {(data ?? []).length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      {data === null ? 'Job tracking is not configured' : 'No recurring jobs found'}
                    </TableCell>
                  </TableRow>
                )}
                {(data ?? []).map((job) => (
                  <TableRow key={job.jobId} hover>
                    <TableCell><Typography variant="body2" sx={{ fontWeight: 500 }}>{job.description || job.jobTypeName}</Typography></TableCell>
                    <TableCell><Typography variant="body2">{job.jobTypeName}</Typography></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap', fontFamily: 'monospace' }}>{job.cronExpression}</Typography></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.lastStarted)}</Typography></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.lastCompleted)}</Typography></TableCell>
                    <TableCell align="right">
                      <Tooltip title={canSubmit ? 'Run now' : 'You do not have permission to trigger jobs'}>
                        <span>
                          <IconButton size="small" onClick={() => runNow(job.jobId)} disabled={triggering === job.jobId || !canSubmit}>
                            {triggering === job.jobId ? <CircularProgress size={16} /> : <PlayArrowIcon fontSize="small" />}
                          </IconButton>
                        </span>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Card>

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert severity="error" onClose={() => setToast(null)} variant="filled">{toast}</Alert>
      </Snackbar>
    </Box>
  );
}
