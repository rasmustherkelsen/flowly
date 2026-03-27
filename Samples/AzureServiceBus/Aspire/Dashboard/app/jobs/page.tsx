'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CircularProgress,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Pagination,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import { JobStatusChip } from '@/components/StatusChip';
import type { Job, PagedResult } from '@/types';

const PAGE_SIZE = 20;
const AUTO_REFRESH_MS = 5000;

const JOB_STATES = ['', 'Created', 'Started', 'Completed', 'Failed'];

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString();
}

function duration(started: string | null, completed: string | null): string {
  if (!started) return '—';
  const end = completed ? new Date(completed) : new Date();
  const ms = end.getTime() - new Date(started).getTime();
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  return `${m}m ${rem}s`;
}

export default function JobsPage() {
  const [result, setResult] = useState<PagedResult<Job> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');

  const load = useCallback(async (p: number, status: string, showSpinner = false) => {
    if (showSpinner) setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({ page: String(p), pageSize: String(PAGE_SIZE), isRecurringJob: 'false' });
      if (status) params.set('status', status);
      const res = await fetch(`/api/jobs?${params}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setResult(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load jobs');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(page, statusFilter, true);
    const interval = setInterval(() => load(page, statusFilter), AUTO_REFRESH_MS);
    return () => clearInterval(interval);
  }, [load, page, statusFilter]);

  const totalPages = result ? Math.ceil(result.totalCount / PAGE_SIZE) : 0;

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3, gap: 2, flexWrap: 'wrap' }}>
        <Typography variant="h5" fontWeight={700} sx={{ flexGrow: 1 }}>
          Jobs
        </Typography>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={statusFilter}
            label="Status"
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          >
            {JOB_STATES.map((s) => (
              <MenuItem key={s} value={s}>{s || 'All'}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <Tooltip title="Refresh">
          <span>
            <IconButton onClick={() => load(page, statusFilter, true)} disabled={loading}>
              <RefreshIcon />
            </IconButton>
          </span>
        </Tooltip>
        {!loading && result && (
          <Typography variant="body2" color="text.secondary">
            {result.totalCount} total
          </Typography>
        )}
      </Box>

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
                  <TableCell>Type</TableCell>
                  <TableCell>Description</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Created</TableCell>
                  <TableCell>Duration</TableCell>
                  <TableCell align="center">Retry</TableCell>
                  <TableCell>Fault</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {result?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      No jobs found
                    </TableCell>
                  </TableRow>
                )}
                {result?.items.map((job) => (
                  <TableRow key={job.jobIdentifier} hover>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>
                        {job.jobTypeName}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {job.description}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <JobStatusChip status={job.currentState} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>
                        {formatDate(job.created)}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>
                        {duration(job.started, job.completed)}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Typography variant="body2">{job.retryAttempt}</Typography>
                    </TableCell>
                    <TableCell>
                      {job.faultReason ? (
                        <Tooltip title={job.faultReason}>
                          <Typography
                            variant="body2"
                            color="error"
                            sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', cursor: 'help' }}
                          >
                            {job.faultReason}
                          </Typography>
                        </Tooltip>
                      ) : (
                        <Typography variant="body2" color="text.disabled">—</Typography>
                      )}
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
          <Pagination
            count={totalPages}
            page={page}
            onChange={(_, p) => setPage(p)}
            color="primary"
          />
        </Box>
      )}
    </Box>
  );
}
