# Overdue photograph cleanup

Operators need to answer three questions: has any acknowledged deletion waited
more than 24 hours for physical removal; is storage still failing; and has cleanup
resumed after recovery?

`cleanup_overdue` (event 3201, Error) is a page-level symptom: at least one pending
deletion is 24 hours old. Its `PendingCount` and `OldestAgeSeconds` describe the
backlog. The threshold comes from L2-032, not a tunable alert preference. The worker
emits this event on maintenance iterations while the condition persists.

`cleanup_media_pending` (event 3202, Warning) identifies a retryable failed removal
by deletion operation ID. JSON scopes carry `EntryPoint=cleanup_worker` and a
`RunId` for correlation. Neither event includes the photograph title, notes,
original filename, media key, owner identity or credentials.

1. Locate event 3201 in worker logs and follow its RunId to related failures.
2. Check that the worker is running and can reach PostgreSQL and the configured
   private media mount. Check mount availability, free space and the worker's
   filesystem permissions. Restore the dependency or permissions that failed.
3. Keep the deletion journal and remaining files intact. Do not clear manifests,
   set completion timestamps manually or restore a content backup to silence the
   alert. Read access remains revoked while cleanup is pending.
4. Leave the worker running. It retries automatically, including after restart,
   and rotates failed records so they do not block other deletions. Verify that
   the affected owner's deletion status becomes Completed and event 3201 stops
   after the overdue backlog clears. A batch contains at most 100 operations.
5. If retries continue to fail after dependencies recover, retain the correlated
   diagnostic events and investigate with the storage/database operator. Do not
   include private photographs or credentials in the incident record.

The local alert signal is the Error event. Deployment must route that signal to
its operational alert view and apply the log-retention policy; delivery through
the final monitoring stack remains a release acceptance gate. This runbook does
not claim that external notifications have been configured or sent.
