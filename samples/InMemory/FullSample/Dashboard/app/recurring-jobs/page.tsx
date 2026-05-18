'use client';

import { useCallback, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CircularProgress,
  IconButton,
  Pagination,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { JobStatusChip } from '@/components/StatusChip';
import PageHeader from '@/components/PageHeader';
import { usePagedData } from '@/hooks/usePagedData';
import { formatDate, duration } from '@/lib/formatters';
import type { Job } from '@/types';

const JOB_STATES = ['', 'Created', 'Started', 'Completed', 'Failed'];

function buildUrl(page: number, filter: string): string {
  const params = new URLSearchParams({ page: String(page), pageSize: '20', isRecurringJob: 'true' });
  if (filter) params.set('status', filter);
  return `/api/jobs?${params}`;
}

export default function RecurringJobsPage() {
  const { result, loading, error, page, setPage, filter, onFilterChange, refresh, totalPages } =
    usePagedData<Job>(buildUrl, 5000);

  const [triggeringJobId, setTriggeringJobId] = useState<string | null>(null);

  const runNow = useCallback(async (jobId: string) => {
    setTriggeringJobId(jobId);
    try {
      const res = await fetch(`/api/run-recurring-job?jobId=${jobId}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      await refresh();
    } catch {
      // error is surfaced via the auto-refresh on next tick
    } finally {
      setTriggeringJobId(null);
    }
  }, [refresh]);

  return (
    <Box>
      <PageHeader
        title="Recurring Jobs"
        filterValue={filter}
        filterOptions={JOB_STATES}
        onFilterChange={onFilterChange}
        loading={loading}
        onRefresh={refresh}
        totalCount={result?.totalCount}
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Card>
        {loading && !result ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
            <CircularProgress />
          </Box>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Description</TableCell>
                  <TableCell>Schedule</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Created</TableCell>
                  <TableCell>Completed</TableCell>
                  <TableCell>Duration</TableCell>
                  <TableCell align="center">Retry</TableCell>
                  <TableCell>Fault</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {result?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      No recurring jobs found
                    </TableCell>
                  </TableRow>
                )}
                {result?.items.map((job) => (
                  <TableRow key={job.jobIdentifier} hover>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>
                        {job.description || job.jobTypeName}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>
                        {job.cronExpression ?? '—'}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <JobStatusChip status={job.currentState} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.created)}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.completed)}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{duration(job.started, job.completed)}</Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2">{job.retryAttempt}</Typography>
                    </TableCell>
                    <TableCell>
                      {job.faultReason ? (
                        <Tooltip title={job.faultReason}>
                          <Typography variant="body2" color="error" sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', cursor: 'help' }}>
                            {job.faultReason}
                          </Typography>
                        </Tooltip>
                      ) : (
                        <Typography variant="body2" color="text.disabled">—</Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Run now">
                        <span>
                          <IconButton
                            size="small"
                            onClick={() => runNow(job.jobIdentifier)}
                            disabled={triggeringJobId === job.jobIdentifier}
                          >
                            {triggeringJobId === job.jobIdentifier
                              ? <CircularProgress size={16} />
                              : <PlayArrowIcon fontSize="small" />}
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

      {totalPages > 1 && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
          <Pagination count={totalPages} page={page} onChange={(_, p) => setPage(p)} color="primary" />
        </Box>
      )}
    </Box>
  );
}
