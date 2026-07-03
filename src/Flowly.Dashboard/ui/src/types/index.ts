export interface JobInfo {
  jobIdentifier: string;
  jobTypeName: string;
  description: string;
  currentState: 'Created' | 'Started' | 'Completed' | 'Failed';
  created: string;
  started: string | null;
  completed: string | null;
  faultReason: string | null;
  retryAttempt: number;
}

export interface RecurringJobInfo {
  jobId: string;
  jobTypeName: string;
  description: string;
  cronExpression: string;
  created: string;
  lastStarted: string | null;
  lastCompleted: string | null;
}

export interface DeadLetter {
  messageId: string;
  queueName: string;
  subscriptionName: string | null;
  messageBody: string;
  messageProperties: string;
  deadLetteredAt: string;
  deadLetterReason: string | null;
  deadLetterErrorDescription: string | null;
  status: 'Pending' | 'Requeued' | 'Discarded';
  requeuedAt: string | null;
  requeuedBy: string | null;
}

export interface SubmitterInfo {
  typeName: string;
  queueOrTopic: string;
  kind: 'message' | 'event' | 'call' | 'job';
  schema: string;
}

export interface DashboardConfig {
  title: string;
  pathPrefix: string;
  hasJobs: boolean;
  hasDeadLetters: boolean;
  hasSubmitters: boolean;
  authEnabled?: boolean;
  canSubmit?: boolean;
  logoutUrl?: string;
}
