import type { ClaimStatus, PolicyStatus } from '../api/types'

// en-IN groups digits the Indian way: 12,34,567 (lakhs and crores).
const rupees = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

export function formatMoney(amount: number): string {
  return rupees.format(amount)
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/** "2026-10-13" becomes "13 Oct 2026". The parts are read directly, so no time zone can shift the day. */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.slice(0, 10).split('-').map(Number)
  const monthName = MONTHS[month - 1]
  if (!year || !monthName || !day) return isoDate
  return `${String(day).padStart(2, '0')} ${monthName} ${year}`
}

/** Shows a UTC timestamp from the API in the viewer's local time. */
export function formatDateTime(timestamp: string): string {
  // A timestamp without a zone would be read as local time, so mark it as UTC.
  const hasZone = /(Z|[+-]\d{2}:\d{2})$/.test(timestamp)
  const date = new Date(hasZone ? timestamp : `${timestamp}Z`)
  if (Number.isNaN(date.getTime())) return timestamp
  return date.toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' })
}

export function statusLabel(status: ClaimStatus | PolicyStatus): string {
  return status === 'UnderReview' ? 'Under review' : status
}
