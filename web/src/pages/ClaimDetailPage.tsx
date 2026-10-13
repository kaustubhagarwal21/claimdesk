import { type ReactNode, useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import { ErrorMessage, Loading } from '../components/Feedback'
import { Panel } from '../components/Panel'
import { StatusBadge } from '../components/StatusBadge'
import { useAsync } from '../hooks/useAsync'
import { formatDate, formatDateTime, formatMoney } from '../lib/format'
import { AuditTrail } from './AuditTrail'
import { ClaimActions } from './ClaimActions'
import { ClaimProgress } from './ClaimProgress'

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-semibold tracking-wide text-muted uppercase">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  )
}

function BackLink() {
  return (
    <Link to="/claims" className="mb-4 inline-block text-sm font-semibold text-brand underline">
      Back to all claims
    </Link>
  )
}

export function ClaimDetailPage() {
  const { id } = useParams()
  const claimId = Number(id)

  const load = useCallback(() => api.getClaim(claimId), [claimId])
  const { data: claim, error, reload } = useAsync(load)

  if (error) {
    return (
      <>
        <BackLink />
        <ErrorMessage
          message={error.status === 404 ? 'This claim does not exist.' : error.message}
          onRetry={error.status === 404 ? undefined : reload}
        />
      </>
    )
  }

  if (!claim) return <Loading label="Loading the claim…" />

  return (
    <>
      <BackLink />

      <div className="mb-6 flex flex-wrap items-center gap-3">
        <h1 className="font-mono text-2xl sm:text-3xl">{claim.claimNumber}</h1>
        <StatusBadge status={claim.status} />
      </div>

      <div className="mb-6 rounded-lg border border-line bg-white p-5">
        <ClaimProgress status={claim.status} />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Panel title="Claim details">
            <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
              <Detail label="Amount claimed">
                <span className="font-mono text-base font-medium">{formatMoney(claim.amount)}</span>
              </Detail>
              <Detail label="Policy">
                <span className="font-mono">{claim.policyNumber}</span> · {claim.policyType}
              </Detail>
              <Detail label="Incident date">{formatDate(claim.incidentDate)}</Detail>
              <Detail label="Filed on">{formatDateTime(claim.createdAt)}</Detail>
              <Detail label="Filed by">{claim.filedBy ?? '—'}</Detail>
              <Detail label="Reviewed by">{claim.reviewedBy ?? '—'}</Detail>
              <Detail label="Approved by">{claim.approvedBy ?? '—'}</Detail>
              {claim.rejectionReason && <Detail label="Rejection reason">{claim.rejectionReason}</Detail>}
              <div className="sm:col-span-2">
                <Detail label="What happened">{claim.description}</Detail>
              </div>
            </dl>
          </Panel>

          <Panel title="Next step">
            <ClaimActions claim={claim} onChanged={reload} />
          </Panel>
        </div>

        <Panel title="Audit trail">
          <AuditTrail entries={claim.audit} />
        </Panel>
      </div>
    </>
  )
}
