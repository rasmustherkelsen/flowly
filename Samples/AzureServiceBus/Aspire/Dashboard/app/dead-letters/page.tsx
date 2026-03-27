'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Pagination,
  Select,
  Snackbar,
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
import ReplayIcon from '@mui/icons-material/Replay';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { DeadLetterStatusChip } from '@/components/StatusChip';
import type { DeadLetter, PagedResult } from '@/types';

const PAGE_SIZE = 20;
const AUTO_REFRESH_MS = 10000;

const STATUSES = ['', 'Pending', 'Requeued'];

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString();
}

interface ConfirmDialog {
  action: 'requeue' | 'discard';
  messageId: string;
  queueName: string;
}

export default function DeadLettersPage() {
  const [result, setResult] = useState<PagedResult<DeadLetter> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [confirm, setConfirm] = useState<ConfirmDialog | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const load = useCallback(async (p: number, status: string, showSpinner = false) => {
    if (showSpinner) setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({ page: String(p), pageSize: String(PAGE_SIZE) });
      if (status) params.set('status', status);
      const res = await fetch(`/api/dead-letters?${params}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setResult(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load dead letters');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(page, statusFilter, true);
    const interval = setInterval(() => load(page, statusFilter), AUTO_REFRESH_MS);
    return () => clearInterval(interval);
  }, [load, page, statusFilter]);

  async function handleConfirm() {
    if (!confirm) return;
    setActionLoading(true);
    try {
      const method = confirm.action === 'discard' ? 'DELETE' : 'POST';
      const res = await fetch(
        `/api/dead-letters/${encodeURIComponent(confirm.messageId)}/${confirm.action}`,
        { method }
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
        throw new Error(body.error ?? `HTTP ${res.status}`);
      }
      const label = confirm.action === 'requeue' ? 'requeued' : 'discarded';
      setToast({ message: `Message ${label} successfully`, severity: 'success' });
      setConfirm(null);
      await load(page, statusFilter, false);
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Action failed', severity: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  const totalPages = result ? Math.ceil(result.totalCount / PAGE_SIZE) : 0;

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3, gap: 2, flexWrap: 'wrap' }}>
        <Typography variant="h5" fontWeight={700} sx={{ flexGrow: 1 }}>
          Dead Letters
        </Typography>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={statusFilter}
            label="Status"
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          >
            {STATUSES.map((s) => (
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
                  <TableCell>Queue</TableCell>
                  <TableCell>Reason</TableCell>
                  <TableCell>Error</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Dead-lettered</TableCell>
                  <TableCell>Requeued</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {result?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      No dead letters found
                    </TableCell>
                  </TableRow>
                )}
                {result?.items.map((dl) => (
                  <TableRow key={dl.messageId} hover>
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>
                        {dl.queueName}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {dl.deadLetterReason ?? '—'}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      {dl.deadLetterErrorDescription ? (
                        <Tooltip title={dl.deadLetterErrorDescription}>
                          <Typography
                            variant="body2"
                            sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', cursor: 'help' }}
                          >
                            {dl.deadLetterErrorDescription}
                          </Typography>
                        </Tooltip>
                      ) : (
                        <Typography variant="body2" color="text.disabled">—</Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <DeadLetterStatusChip status={dl.status} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>
                        {formatDate(dl.deadLetteredAt)}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      {dl.requeuedAt ? (
                        <Tooltip title={dl.requeuedBy ? `by ${dl.requeuedBy}` : ''}>
                          <Typography variant="body2" sx={{ whiteSpace: 'nowrap', cursor: dl.requeuedBy ? 'help' : 'default' }}>
                            {formatDate(dl.requeuedAt)}
                          </Typography>
                        </Tooltip>
                      ) : (
                        <Typography variant="body2" color="text.disabled">—</Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {dl.status === 'Pending' && (
                        <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'flex-end' }}>
                          <Tooltip title="Requeue">
                            <IconButton
                              size="small"
                              color="primary"
                              onClick={() => setConfirm({ action: 'requeue', messageId: dl.messageId, queueName: dl.queueName })}
                            >
                              <ReplayIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Discard">
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => setConfirm({ action: 'discard', messageId: dl.messageId, queueName: dl.queueName })}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </Box>
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

      <Dialog open={!!confirm} onClose={() => !actionLoading && setConfirm(null)}>
        <DialogTitle>
          {confirm?.action === 'requeue' ? 'Requeue message?' : 'Discard message?'}
        </DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirm?.action === 'requeue'
              ? `The message will be sent back to queue "${confirm?.queueName}" for processing.`
              : `The message from queue "${confirm?.queueName}" will be permanently deleted.`}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirm(null)} disabled={actionLoading}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            color={confirm?.action === 'discard' ? 'error' : 'primary'}
            variant="contained"
            disabled={actionLoading}
            startIcon={actionLoading ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {confirm?.action === 'requeue' ? 'Requeue' : 'Discard'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={!!toast}
        autoHideDuration={4000}
        onClose={() => setToast(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={toast?.severity} onClose={() => setToast(null)} variant="filled">
          {toast?.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
