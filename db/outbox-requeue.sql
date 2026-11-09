\set ON_ERROR_STOP on
\if :{?event_id}
\else
DO $fail$ BEGIN RAISE EXCEPTION 'event_id wajib diisi'; END $fail$;
\endif
\if :{?operator}
\else
DO $fail$ BEGIN RAISE EXCEPTION 'operator wajib diisi'; END $fail$;
\endif
\if :{?reason}
\else
DO $fail$ BEGIN RAISE EXCEPTION 'reason wajib diisi'; END $fail$;
\endif

BEGIN;
WITH replay AS (
    UPDATE outbox_messages
    SET "Status"='pending',
        "AttemptCount"=0,
        "AvailableAtUtc"=now(),
        "LockId"=NULL,
        "LockedAtUtc"=NULL,
        "LockedBy"=NULL,
        "ProcessedAtUtc"=NULL,
        "DeadLetteredAtUtc"=NULL,
        "ManualReplayCount"="ManualReplayCount"+1,
        "LastReplayedAtUtc"=now(),
        "LastReplayedBy"=left(:'operator',150),
        "LastReplayReason"=left(:'reason',500)
    WHERE "Id"=:'event_id'::uuid
      AND "Status" IN ('failed','dead_letter')
    RETURNING 1
)
SELECT (count(*)=1) AS requeued FROM replay \gset

\if :requeued
COMMIT;
SELECT "Id","EventType","Status","AttemptCount","ManualReplayCount","AvailableAtUtc"
FROM outbox_messages WHERE "Id"=:'event_id'::uuid;
\else
ROLLBACK;
DO $fail$
BEGIN
    RAISE EXCEPTION 'Event tidak ditemukan atau status bukan failed/dead_letter';
END
$fail$;
\endif
