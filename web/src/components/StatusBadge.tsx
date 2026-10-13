import type { ClaimStatus, PolicyStatus } from '../api/types'
import { statusLabel } from '../lib/format'

type Status = ClaimStatus | PolicyStatus

// Each status has a background, a text colour and a dot colour.
// The label is always shown, so the colour is never the only signal.
const STATUS_CLASSES: Record<Status, { badge: string; dot: string }> = {
  Submitted: { badge: 'bg-slate-200 text-slate-800', dot: 'bg-slate-500' },
  UnderReview: { badge: 'bg-amber-100 text-amber-900', dot: 'bg-amber-500' },
  Approved: { badge: 'bg-emerald-100 text-emerald-900', dot: 'bg-emerald-600' },
  Rejected: { badge: 'bg-red-100 text-red-900', dot: 'bg-red-600' },
  Paid: { badge: 'bg-indigo-100 text-indigo-900', dot: 'bg-indigo-600' },
  Active: { badge: 'bg-emerald-100 text-emerald-900', dot: 'bg-emerald-600' },
  Cancelled: { badge: 'bg-red-100 text-red-900', dot: 'bg-red-600' },
  Expired: { badge: 'bg-slate-200 text-slate-800', dot: 'bg-slate-500' },
}

export function StatusBadge({ status }: { status: Status }) {
  const classes = STATUS_CLASSES[status]
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap ${classes.badge}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${classes.dot}`} />
      {statusLabel(status)}
    </span>
  )
}
