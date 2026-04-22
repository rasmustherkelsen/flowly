'use client';

import { Chip } from '@mui/material';

type JobStatus = 'Created' | 'Started' | 'Completed' | 'Failed';
type DeadLetterStatus = 'Pending' | 'Requeued';

const JOB_STATUS_COLOR: Record<JobStatus, 'default' | 'info' | 'success' | 'error'> = {
  Created: 'default',
  Started: 'info',
  Completed: 'success',
  Failed: 'error',
};

const DEAD_LETTER_STATUS_COLOR: Record<DeadLetterStatus, 'warning' | 'success'> = {
  Pending: 'warning',
  Requeued: 'success',
};

export function JobStatusChip({ status }: { status: string }) {
  const color = JOB_STATUS_COLOR[status as JobStatus] ?? 'default';
  return <Chip label={status} color={color} size="small" variant="outlined" />;
}

export function DeadLetterStatusChip({ status }: { status: string }) {
  const color = DEAD_LETTER_STATUS_COLOR[status as DeadLetterStatus] ?? 'default';
  return <Chip label={status} color={color} size="small" variant="outlined" />;
}
