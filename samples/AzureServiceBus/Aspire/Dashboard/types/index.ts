export interface Job {
  jobIdentifier: string;
  jobTypeName: string;
  description: string;
  currentState: 'Created' | 'Started' | 'Completed' | 'Failed';
  created: string;
  started: string | null;
  completed: string | null;
  faultReason: string | null;
  retryAttempt: number;
  isRecurringJob: boolean;
  cronExpression: string | null;
}

export interface DeadLetter {
  messageId: string;
  queueName: string;
  messageBody: string;
  messageProperties: string;
  deadLetteredAt: string;
  deadLetterReason: string | null;
  deadLetterErrorDescription: string | null;
  status: 'Pending' | 'Requeued';
  requeuedAt: string | null;
  requeuedBy: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
