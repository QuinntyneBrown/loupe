export interface OperationResult {
  id: string;
  resourceId: string;
  type: 'Critique';
  status: 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Canceled';
  mode: 'Demo' | 'Live';
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  nextAttemptAt: string | null;
  retryAvailableAt: string | null;
  failureCode: string | null;
  message: string;
}
