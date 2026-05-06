'use client';

import { useState } from 'react';
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
  IconButton,
  Pagination,
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
import ReplayIcon from '@mui/icons-material/Replay';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import { DeadLetterStatusChip } from '@/components/StatusChip';
import PageHeader from '@/components/PageHeader';
import { usePagedData } from '@/hooks/usePagedData';
import { formatDate } from '@/lib/formatters';
import type { DeadLetter } from '@/types';

const STATUSES = ['', 'Pending', 'Requeued'];

interface ConfirmDialog {
  action: 'requeue' | 'discard';
  messageId: string;
  queueName: string;
}

function tryPrettyPrint(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function buildUrl(page: number, filter: string): string {
  const params = new URLSearchParams({ page: String(page), pageSize: '20' });
  if (filter) params.set('status', filter);
  return `/api/dead-letters?${params}`;
}

export default function DeadLettersPage() {
  const { result, loading, error, page, setPage, filter, onFilterChange, refresh, totalPages } =
    usePagedData<DeadLetter>(buildUrl, 10000);

  const [confirm, setConfirm] = useState<ConfirmDialog | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);
  const [inspecting, setInspecting] = useState<DeadLetter | null>(null);

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
      await refresh();
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Action failed', severity: 'error' });
    } finally {
      setActionLoading(false);
    }
  }

  return (
    <Box>
      <PageHeader
        title="Dead Letters"
        filterValue={filter}
        filterOptions={STATUSES}
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
                      <Typography variant="body2" fontWeight={500}>{dl.queueName}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {dl.deadLetterReason ?? '—'}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      {dl.deadLetterErrorDescription ? (
                        <Tooltip title={dl.deadLetterErrorDescription}>
                          <Typography variant="body2" sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', cursor: 'help' }}>
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
                      <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(dl.deadLetteredAt)}</Typography>
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
                      <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'flex-end' }}>
                        <Tooltip title="Inspect payload">
                          <IconButton size="small" onClick={() => setInspecting(dl)}>
                            <VisibilityOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {dl.status === 'Pending' && (
                          <>
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
                          </>
                        )}
                      </Box>
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

      <Dialog open={!!inspecting} onClose={() => setInspecting(null)} maxWidth="md" fullWidth>
        <DialogTitle>
          Payload — {inspecting?.queueName}
        </DialogTitle>
        <DialogContent dividers>
          <Typography variant="overline" color="text.secondary" display="block" gutterBottom>
            Message Body
          </Typography>
          <Box
            component="pre"
            sx={{
              fontFamily: 'monospace',
              fontSize: '0.8rem',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              bgcolor: 'action.hover',
              borderRadius: 1,
              p: 1.5,
              mb: 3,
              maxHeight: 300,
              overflow: 'auto',
            }}
          >
            {inspecting ? tryPrettyPrint(inspecting.messageBody) : ''}
          </Box>
          <Typography variant="overline" color="text.secondary" display="block" gutterBottom>
            Message Properties
          </Typography>
          <Box
            component="pre"
            sx={{
              fontFamily: 'monospace',
              fontSize: '0.8rem',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              bgcolor: 'action.hover',
              borderRadius: 1,
              p: 1.5,
              maxHeight: 200,
              overflow: 'auto',
            }}
          >
            {inspecting ? tryPrettyPrint(inspecting.messageProperties) : ''}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInspecting(null)}>Close</Button>
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
