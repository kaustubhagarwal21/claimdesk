import { useCallback, useState } from 'react'
import { api, toApiError } from '../api/client'
import { POLICY_STATUSES, type Policy, type PolicyStatus } from '../api/types'
import { useCurrentUser } from '../auth/UserContext'
import { canManagePolicies } from '../auth/permissions'
import { Button } from '../components/Button'
import { type Column, DataTable } from '../components/DataTable'
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback'
import { SelectField } from '../components/Field'
import { PageHeader, Panel } from '../components/Panel'
import { StatusBadge } from '../components/StatusBadge'
import { useAsync } from '../hooks/useAsync'
import { formatDate, formatMoney } from '../lib/format'
import { CustomerForm } from './CustomerForm'
import { PolicyForm } from './PolicyForm'

export function PoliciesPage() {
  const { user } = useCurrentUser()
  const mayManage = canManagePolicies(user.role)

  const [status, setStatus] = useState<PolicyStatus | ''>('')
  const [cancelError, setCancelError] = useState<string | null>(null)

  const loadPolicies = useCallback(() => api.getPolicies({ status: status || undefined }), [status])
  const policies = useAsync(loadPolicies)
  const customers = useAsync(api.getCustomers)

  async function cancelPolicy(policy: Policy) {
    if (!window.confirm(`Cancel policy ${policy.policyNumber}? This cannot be undone.`)) return
    setCancelError(null)
    try {
      await api.cancelPolicy(policy.id)
    } catch (err) {
      setCancelError(toApiError(err).message)
    }
    // Reload either way: after a failure the list shows what the server has now.
    policies.reload()
  }

  const columns: Column<Policy>[] = [
    { header: 'Policy no.', cell: (policy) => <span className="font-mono">{policy.policyNumber}</span> },
    { header: 'Customer', cell: (policy) => policy.customerName },
    { header: 'Type', cell: (policy) => policy.type },
    { header: 'Term', cell: (policy) => `${formatDate(policy.startDate)} to ${formatDate(policy.endDate)}` },
    { header: 'Sum insured', cell: (policy) => formatMoney(policy.sumInsured), align: 'right' },
    { header: 'Premium', cell: (policy) => formatMoney(policy.premium), align: 'right' },
    { header: 'Remaining cover', cell: (policy) => formatMoney(policy.remainingCover), align: 'right' },
    { header: 'Status', cell: (policy) => <StatusBadge status={policy.status} /> },
  ]
  if (mayManage) {
    columns.push({
      header: 'Action',
      cell: (policy) =>
        policy.status === 'Active' ? (
          <Button
            variant="secondary"
            small
            aria-label={`Cancel policy ${policy.policyNumber}`}
            onClick={() => cancelPolicy(policy)}
          >
            Cancel
          </Button>
        ) : null,
    })
  }

  return (
    <>
      <PageHeader title="Policies">
        Get a quote, issue a policy and see every policy with the cover it has left.
      </PageHeader>

      <div className="mb-8 grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <Panel title={mayManage ? 'Quote and issue a policy' : 'Get a quote'}>
            {customers.error ? (
              <ErrorMessage message={customers.error.message} onRetry={customers.reload} />
            ) : !customers.data ? (
              <Loading label="Loading customers…" />
            ) : (
              <PolicyForm customers={customers.data} onIssued={policies.reload} />
            )}
          </Panel>
        </div>
        {mayManage && (
          <Panel title="Add a customer">
            <CustomerForm onCreated={customers.reload} />
          </Panel>
        )}
      </div>

      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <h2 className="text-lg">All policies</h2>
        <div className="w-44">
          <SelectField label="Status" value={status} onChange={(e) => setStatus(e.target.value as PolicyStatus | '')}>
            <option value="">All statuses</option>
            {POLICY_STATUSES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </SelectField>
        </div>
      </div>

      {cancelError && (
        <div className="mb-4">
          <ErrorMessage message={cancelError} />
        </div>
      )}

      {policies.error ? (
        <ErrorMessage message={policies.error.message} onRetry={policies.reload} />
      ) : !policies.data ? (
        <Loading label="Loading policies…" />
      ) : policies.data.length === 0 ? (
        <EmptyState title="No policies to show">
          {status ? 'No policy has this status. Choose another one.' : 'Issue the first policy with the form above.'}
        </EmptyState>
      ) : (
        <DataTable caption="Policies" columns={columns} rows={policies.data} rowKey={(policy) => policy.id} />
      )}
    </>
  )
}
