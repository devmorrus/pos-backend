# Runbook: Outbox Backlog, Retry, dan Dead Letter

1. Buka dashboard **MorrusPOS Queue Operations** dan periksa pending, failed, dead-letter, oldest age, serta delivery outcomes untuk queue `outbox`.
2. Pastikan API, PostgreSQL, dan queue monitor sehat. Outbox tidak memblokir readiness POS.
3. Cari log `OutboxDeliveryFailed`, `OutboxDispatcherLoopFailed`, atau `OutboxLeasesRecovered` berdasarkan `EventId`; jangan mencetak `PayloadJson`.
4. Untuk `OUTBOX_HANDLER_TIMEOUT`/`OUTBOX_DELIVERY_FAILED`, periksa SignalR dan dependency handler. Automatic retry akan berjalan dengan exponential backoff.
5. Untuk `OUTBOX_HANDLER_NOT_FOUND`, deploy handler/schema version yang benar sebelum replay.
6. Untuk `OUTBOX_INVALID_PAYLOAD`, perbaiki producer. Jangan replay poison event berulang kali.
7. Inspect metadata tanpa payload:
   ```sql
   SELECT "Id","EventType","SchemaVersion","Status","AttemptCount","MaxAttempts",
          "OccurredAtUtc","AvailableAtUtc","LastErrorCode","DeadLetteredAtUtc"
   FROM outbox_messages
   WHERE "Status" IN ('failed','dead_letter')
   ORDER BY "SequenceNo";
   ```
8. Setelah root cause selesai, replay satu event:
   ```bash
   scripts/requeue-outbox.sh EVENT_UUID operator.name 'root cause sudah diperbaiki'
   ```
9. Konfirmasi event berubah `pending -> processing -> processed`, backlog turun, dan tidak ada dead-letter baru.
10. Jangan menghapus row, mengedit payload, menurunkan attempt count lewat SQL manual, atau melakukan bulk replay untuk menghilangkan alert.

Catat EventId, error code, root cause, operator replay, hasil delivery, dan waktu pemulihan pada incident record.
