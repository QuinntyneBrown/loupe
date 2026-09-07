export interface DeletionResult {
  id: string;
  resourceId: string;
  status: 'Pending' | 'Completed';
  deletedAt: string;
  completedAt: string | null;
}
