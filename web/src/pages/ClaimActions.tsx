import { type SubmitEvent, useState } from 'react'
import { api, toApiError } from '../api/client'
import type { Claim } from '../api/types'
import { useCurrentUser } from '../auth/UserContext'
import { getClaimActions } from '../auth/permissions'
import { Button } from '../components/Button'
import { ErrorMessage, SuccessMessage } from '../components/Feedback'
import { TextAreaField } from '../components/Field'

interface ClaimActionsProps {
  claim: Claim
  /** Called after the claim changed on the server (or may have), so the page reloads it. */
  onChanged: () => void
}

/** The review, approve, reject and pay buttons. Only the ones the current user may use are shown. */
export function ClaimActions({ claim, onChanged }: ClaimActionsProps) {
  const { user } = useCurrentUser()
  const { actions, note } = getClaimActions(user, claim)

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string>()

  async function run(action: () => Promise<Claim>, doneMessage: string) {
    setBusy(true)
    setError(null)
    setDone(null)
    try {
      await action()
      setDone(doneMessage)
      setRejecting(false)
      setReason('')
      onChanged()
    } catch (err) {
      const apiError = toApiError(err)
      if (apiError.fieldError('reason')) {
        setReasonError(apiError.fieldError('reason'))
      } else if (apiError.status === 409) {
        // A business rule was broken, or someone else changed the claim first.
        // Reload it so the user sees the current status and gets a fresh rowVersion.
        setError(`${apiError.message} The claim has been reloaded with its latest details.`)
        setRejecting(false)
        onChanged()
      } else {
        setError(apiError.message)
      }
    } finally {
      setBusy(false)
    }
  }

  function submitRejection(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmed = reason.trim()
    if (trimmed.length < 5 || trimmed.length > 300) {
      setReasonError('Give a reason of 5 to 300 characters.')
      return
    }
    void run(() => api.rejectClaim(claim.id, trimmed, claim.rowVersion), 'Claim rejected.')
  }

  return (
    <div className="space-y-4">
      {error && <ErrorMessage message={error} />}
      {done && <SuccessMessage>{done}</SuccessMessage>}
      {note && <p className="text-sm text-muted">{note}</p>}

      {!rejecting && actions.length > 0 && (
        <div className="flex flex-wrap gap-3">
          {actions.includes('review') && (
            <Button disabled={busy} onClick={() => run(() => api.reviewClaim(claim.id), 'Review started.')}>
              Start review
            </Button>
          )}
          {actions.includes('approve') && (
            <Button
              disabled={busy}
              onClick={() => run(() => api.approveClaim(claim.id, claim.rowVersion), 'Claim approved.')}
            >
              Approve
            </Button>
          )}
          {actions.includes('reject') && (
            <Button variant="danger" disabled={busy} onClick={() => setRejecting(true)}>
              Reject
            </Button>
          )}
          {actions.includes('pay') && (
            <Button
              disabled={busy}
              onClick={() => run(() => api.payClaim(claim.id, claim.rowVersion), 'Claim marked as paid.')}
            >
              Mark as paid
            </Button>
          )}
        </div>
      )}

      {rejecting && (
        <form onSubmit={submitRejection} noValidate className="space-y-3">
          <TextAreaField
            label="Reason for rejection"
            maxLength={300}
            value={reason}
            error={reasonError}
            hint="The reason is saved with the claim. 5 to 300 characters."
            onChange={(e) => {
              setReason(e.target.value)
              setReasonError(undefined)
            }}
          />
          <div className="flex flex-wrap gap-3">
            <Button type="submit" variant="danger" disabled={busy}>
              Confirm rejection
            </Button>
            <Button variant="secondary" disabled={busy} onClick={() => setRejecting(false)}>
              Keep under review
            </Button>
          </div>
        </form>
      )}
    </div>
  )
}
