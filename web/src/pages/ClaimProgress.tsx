import type { ClaimStatus } from '../api/types'
import { statusLabel } from '../lib/format'

// The two ways a claim can travel through the workflow in docs/API.md.
const PAID_PATH: ClaimStatus[] = ['Submitted', 'UnderReview', 'Approved', 'Paid']
const REJECTED_PATH: ClaimStatus[] = ['Submitted', 'UnderReview', 'Rejected']

/** Shows where a claim is in its workflow: steps done, the current step, and what is left. */
export function ClaimProgress({ status }: { status: ClaimStatus }) {
  const steps = status === 'Rejected' ? REJECTED_PATH : PAID_PATH
  const currentIndex = steps.indexOf(status)

  return (
    <ol aria-label="Claim progress" className="flex gap-1.5">
      {steps.map((step, index) => {
        const isCurrent = index === currentIndex
        const isDone = index < currentIndex
        const barColour = isCurrent ? (status === 'Rejected' ? 'bg-red-600' : 'bg-marigold') : isDone ? 'bg-ink' : 'bg-line'

        return (
          <li key={step} aria-current={isCurrent ? 'step' : undefined} className="min-w-0 flex-1">
            <span aria-hidden="true" className={`block h-1.5 rounded-full ${barColour}`} />
            <span className={`mt-1.5 block text-xs ${isCurrent ? 'font-bold' : isDone ? 'font-medium' : 'text-muted'}`}>
              {statusLabel(step)}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
