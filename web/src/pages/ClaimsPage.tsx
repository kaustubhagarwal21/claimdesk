import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { CLAIM_STATUSES, type Claim, type ClaimStatus } from '../api/types'
import { useCurrentUser } from '../auth/UserContext'
import { canFileClaim } from '../auth/permissions'
import { type Column, DataTable } from '../components/DataTable'
import { EmptyState, ErrorMessage, Loading, SuccessMessage } from '../components/Feedback'
import { SelectField } from '../components/Field'
import { PageHeader, Panel } from '../components/Panel'
import { StatusBadge } from '../components/StatusBadge'
import { useAsync } from '../hooks/useAsync'
import { formatDate, formatMoney, statusLabel } from '../lib/format'
import { ClaimForm } from './ClaimForm'

const COLUMNS: Column<Claim>[] = [
  {
    header: 'Claim no.',
    cell: (claim) => (
      <Link to={`/claims/${claim.id}`} className="font-mono font-medium text-brand underline">
        {claim.claimNumber}
      </Link>
    ),
  },
  { header: 'Policy no.', cell: (claim) => <span className="font-mono">{claim.policyNumber}</span> },
  { header: 'Type', cell: (claim) => claim.policyType },
  { header: 'Incident date', cell: (claim) => formatDate(claim.incidentDate) },
  { header: 'Amount', cell: (claim) => formatMoney(claim.amount), align: 'right' },
  { header: 'Status', cell: (claim) => <StatusBadge status={claim.status} /> },
  { header: 'Filed by', cell: (claim) => claim.filedBy ?? '—' },
]

export function ClaimsPage() {
  const { user } = useCurrentUser()

  const [status, setStatus] = useState<ClaimStatus | ''>('')
  const [filed, setFiled] = useState<Claim | null>(null)

  const loadClaims = useCallback(() => api.getClaims({ status: status || undefined }), [status])
  const { data: claims, error, reload } = useAsync(loadClaims)

  function handleFiled(claim: Claim) {
    setFiled(claim)
    reload()
  }

  return (
    <>
      <PageHeader title="Claims">
        File a claim on an active policy and follow it from submission to payment.
      </PageHeader>

      <div className="mb-8 max-w-3xl">
        {canFileClaim(user.role) ? (
          <Panel title="File a claim">
            {filed && (
              <div className="mb-4">
                <SuccessMessage>
                  Claim{' '}
                  <Link to={`/claims/${filed.id}`} className="font-semibold underline">
                    {filed.claimNumber}
                  </Link>{' '}
                  filed. An adjuster will review it next.
                </SuccessMessage>
              </div>
            )}
            <ClaimForm onFiled={handleFiled} />
          </Panel>
        ) : (
          <p className="rounded-md border border-line bg-white px-4 py-3 text-sm text-muted">
            Only an agent can file a claim. As {user.role === 'Adjuster' ? 'an adjuster' : 'a manager'}, open a claim
            below to act on it.
          </p>
        )}
      </div>

      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <h2 className="text-lg">All claims</h2>
        <div className="w-44">
          <SelectField label="Status" value={status} onChange={(e) => setStatus(e.target.value as ClaimStatus | '')}>
            <option value="">All statuses</option>
            {CLAIM_STATUSES.map((option) => (
              <option key={option} value={option}>
                {statusLabel(option)}
              </option>
            ))}
          </SelectField>
        </div>
      </div>

      {error ? (
        <ErrorMessage message={error.message} onRetry={reload} />
      ) : !claims ? (
        <Loading label="Loading claims…" />
      ) : claims.length === 0 ? (
        <EmptyState title="No claims to show">
          {status ? 'No claim has this status. Choose another one.' : 'No claim has been filed yet.'}
        </EmptyState>
      ) : (
        <DataTable caption="Claims" columns={COLUMNS} rows={claims} rowKey={(claim) => claim.id} />
      )}
    </>
  )
}
