export function critiqueOperation(resourceId, status = 'Queued') {
  return {
    id: '10000000-0000-4000-8000-000000000001', resourceId, type: 'Critique', status, mode: 'Live',
    createdAt: '2026-09-07T13:00:00Z', updatedAt: '2026-09-07T13:00:00Z',
    completedAt: null, nextAttemptAt: null, retryAvailableAt: null, failureCode: null,
    message: status === 'Queued' ? 'Waiting to start.' : 'Analyzing the photograph.',
  };
}
