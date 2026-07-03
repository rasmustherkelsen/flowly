import { useCallback, useState } from 'react';
import {
  Alert, Box, Button, Card, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogContentText, DialogTitle, IconButton, Pagination, Snackbar, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Tooltip, Typography,
} from '@mui/material';
import ReplayIcon from '@mui/icons-material/Replay';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import { DeadLetterStatusChip } from '../components/StatusChip';
import PageHeader from '../components/PageHeader';
import { useAutoRefresh } from '../hooks/useAutoRefresh';
import { useCanSubmit } from '../hooks/useCanSubmit';
import { formatDate, tryPrettyPrint } from '../lib/formatters';
import type { DeadLetter } from '../types';

const PAGE_SIZE = 20;
const STATUSES = ['', 'Pending', 'Requeued', 'Discarded'];

interface Confirm { action: 'requeue' | 'discard'; messageId: string; queueName: string; }

export default function DeadLettersPage() {
  const canSubmit = useCanSubmit();
  const [filter, setFilter] = useState('');
  const [page, setPage] = useState(1);
  const [confirm, setConfirm] = useState<Confirm | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);
  const [inspecting, setInspecting] = useState<DeadLetter | null>(null);

  const fetchFn = useCallback(async () => {
    const res = await fetch('api/dead-letters');
    if (!res.ok) {
      if (res.status === 404) return [] as DeadLetter[];
      throw new Error(`HTTP ${res.status}`);
    }
    return res.json() as Promise<DeadLetter[]>;
  }, []);

  const { data, loading, error, refresh } = useAutoRefresh(fetchFn, 10000);

  const filtered = (data ?? []).filter((dl) => !filter || dl.status === filter);
  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const items = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const handleConfirm = async () => {
    if (!confirm) return;
    setActionLoading(true);
    try {
      const method = confirm.action === 'discard' ? 'DELETE' : 'POST';
      const res = await fetch(`api/dead-letters/${encodeURIComponent(confirm.messageId)}/${confirm.action}`, { method });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setToast({ message: `Message ${confirm.action === 'requeue' ? 'requeued' : 'discarded'} successfully`, severity: 'success' });
      setConfirm(null);
      await refresh();
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Action failed', severity: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Dead Letters"
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
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      {data === null ? 'Dead letter tracking is not configured' : 'No dead letters found'}
                    </TableCell>
                  </TableRow>
                )}
                {items.map((dl) => (
                  <TableRow key={dl.messageId} hover>
                    <TableCell><Typography variant="body2" sx={{ fontWeight: 500 }}>{dl.queueName}</Typography></TableCell>
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
                      ) : <Typography variant="body2" color="text.disabled">—</Typography>}
                    </TableCell>
                    <TableCell><DeadLetterStatusChip status={dl.status} /></TableCell>
                    <TableCell><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>{formatDate(dl.deadLetteredAt)}</Typography></TableCell>
                    <TableCell>
                      {dl.requeuedAt ? (
                        <Tooltip title={dl.requeuedBy ? `by ${dl.requeuedBy}` : ''}>
                          <Typography variant="body2" sx={{ whiteSpace: 'nowrap', cursor: dl.requeuedBy ? 'help' : 'default' }}>
                            {formatDate(dl.requeuedAt)}
                          </Typography>
                        </Tooltip>
                      ) : <Typography variant="body2" color="text.disabled">—</Typography>}
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
                            <Tooltip title={canSubmit ? 'Requeue' : 'You do not have permission to requeue messages'}>
                              <span>
                                <IconButton size="small" color="primary" disabled={!canSubmit} onClick={() => setConfirm({ action: 'requeue', messageId: dl.messageId, queueName: dl.queueName })}>
                                  <ReplayIcon fontSize="small" />
                                </IconButton>
                              </span>
                            </Tooltip>
                            <Tooltip title={canSubmit ? 'Discard' : 'You do not have permission to discard messages'}>
                              <span>
                                <IconButton size="small" color="error" disabled={!canSubmit} onClick={() => setConfirm({ action: 'discard', messageId: dl.messageId, queueName: dl.queueName })}>
                                  <DeleteOutlineIcon fontSize="small" />
                                </IconButton>
                              </span>
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
        <DialogTitle>{confirm?.action === 'requeue' ? 'Requeue message?' : 'Discard message?'}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirm?.action === 'requeue'
              ? `The message will be sent back to "${confirm?.queueName}" for processing.`
              : `The message from "${confirm?.queueName}" will be permanently deleted.`}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirm(null)} disabled={actionLoading}>Cancel</Button>
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
        <DialogTitle>Payload — {inspecting?.queueName}</DialogTitle>
        <DialogContent dividers>
          <Typography variant="overline" color="text.secondary" sx={{ display: 'block' }} gutterBottom>Message Body</Typography>
          <Box component="pre" sx={{ fontFamily: 'monospace', fontSize: '0.8rem', whiteSpace: 'pre-wrap', wordBreak: 'break-all', bgcolor: 'action.hover', borderRadius: 1, p: 1.5, mb: 3, maxHeight: 300, overflow: 'auto' }}>
            {inspecting ? tryPrettyPrint(inspecting.messageBody) : ''}
          </Box>
          <Typography variant="overline" color="text.secondary" sx={{ display: 'block' }} gutterBottom>Message Properties</Typography>
          <Box component="pre" sx={{ fontFamily: 'monospace', fontSize: '0.8rem', whiteSpace: 'pre-wrap', wordBreak: 'break-all', bgcolor: 'action.hover', borderRadius: 1, p: 1.5, maxHeight: 200, overflow: 'auto' }}>
            {inspecting ? tryPrettyPrint(inspecting.messageProperties) : ''}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInspecting(null)}>Close</Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert severity={toast?.severity} onClose={() => setToast(null)} variant="filled">{toast?.message}</Alert>
      </Snackbar>
    </Box>
  );
}
