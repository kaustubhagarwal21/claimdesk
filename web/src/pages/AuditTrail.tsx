import type { AuditEntry } from '../api/types'
import { formatDateTime, statusLabel } from '../lib/format'

/** The status history of a claim. The rows are written by a database trigger, not by the app. */
export function AuditTrail({ entries }: { entries: AuditEntry[] }) {
  if (entries.length === 0) {
    return <p className="text-sm text-muted">No status changes have been recorded yet.</p>
  }

  return (
    <ol aria-label="Audit trail" className="ml-1.5 space-y-5 border-l-2 border-line">
      {entries.map((entry) => (
        <li key={`${entry.changedAt}-${entry.newStatus}`} className="relative pl-5">
          <span aria-hidden="true" className="absolute top-1 -left-[7px] size-3 rounded-full border-2 border-white bg-ink" />
          <p className="text-sm font-semibold">
            {entry.oldStatus
              ? `${statusLabel(entry.oldStatus)} → ${statusLabel(entry.newStatus)}`
              : `Filed as ${statusLabel(entry.newStatus)}`}
          </p>
          <p className="text-xs text-muted">
            {entry.changedBy ?? 'Unknown user'} · <time dateTime={entry.changedAt}>{formatDateTime(entry.changedAt)}</time>
          </p>
        </li>
      ))}
    </ol>
  )
}
