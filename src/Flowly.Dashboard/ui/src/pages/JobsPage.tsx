import { useCallback, useState } from 'react';
import {
  Alert, Box, Card, CircularProgress, Pagination, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Tooltip, Typography,
} from '@mui/material';
import { JobStatusChip } from '../components/StatusChip';
import PageHeader from '../components/PageHeader';
import { useAutoRefresh } from '../hooks/useAutoRefresh';
import { formatDate, duration } from '../lib/formatters';
import type { JobInfo } from '../types';

const PAGE_SIZE = 20;
const STATUSES = ['', 'Created', 'Started', 'Completed', 'Failed'];

export default function JobsPage() {
  const [filter, setFilter] = useState('');
  const [page, setPage] = useState(1);

  const fetchFn = useCallback(async () => {
    const res = await fetch('api/jobs');
    if (!res.ok) {
      if (res.status === 404) return [] as JobInfo[];
      throw new Error(`HTTP ${res.status}`);
    }
    return res.json() as Promise<JobInfo[]>;
  }, []);

  const { data, loading, error, refresh } = useAutoRefresh(fetchFn, 5000);

  const filtered = (data ?? []).filter((j) => !filter || j.currentState === filter);
  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const items = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <Box>
      <PageHeader
        title="Jobs"
        filterValue={filter}
        filterOptions={STATUSES}
        onFilterChange={(f) => { setFilter(f); setPage(1); }}
        loading={loading}
        onRefresh={refresh}
        totalCount={filtered.length}
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
                  <TableCell>Type</TableCell>
                  <TableCell>Description</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Created</TableCell>
                  <TableCell>Completed</TableCell>
                  <TableCell>Duration</TableCell>
                  <TableCell align="center">Retry</TableCell>
                  <TableCell>Fault</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={8} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      {data === null ? 'Job tracking is not configured' : 'No jobs found'}
                    </TableCell>
                  </TableRow>
                )}
                {items.map((job) => (
                  <TableRow key={job.jobIdentifier} hover>
                    <TableCell><Typography variant="body2" fontWeight={500}>{job.jobTypeName}</Typography></TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {job.description}
                      </Typography>
                    </TableCell>
                    <TableCell><JobStatusChip status={job.currentState} /></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.created)}</Typography></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(job.completed)}</Typography></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{duration(job.started, job.completed)}</Typography></TableCell>
                    <TableCell align="center"><Typography variant="body2">{job.retryAttempt}</Typography></TableCell>
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
