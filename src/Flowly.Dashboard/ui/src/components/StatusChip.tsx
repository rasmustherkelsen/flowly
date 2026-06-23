import { Chip } from '@mui/material';

const JOB_COLORS: Record<string, 'default' | 'info' | 'success' | 'error'> = {
  Created: 'default',
  Started: 'info',
  Completed: 'success',
  Failed: 'error',
};

const DL_COLORS: Record<string, 'warning' | 'success' | 'default'> = {
  Pending: 'warning',
  Requeued: 'success',
  Discarded: 'default',
};

export function JobStatusChip({ status }: { status: string }) {
  return <Chip label={status} color={JOB_COLORS[status] ?? 'default'} size="small" variant="outlined" />;
}

export function DeadLetterStatusChip({ status }: { status: string }) {
  return <Chip label={status} color={DL_COLORS[status] ?? 'default'} size="small" variant="outlined" />;
}
