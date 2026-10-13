import { useCallback, useState } from 'react'
import { api } from '../api/client'
import type { ClaimStatus, ClaimsSummaryRow } from '../api/types'
import { type Column, DataTable } from '../components/DataTable'
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback'
import { TextField } from '../components/Field'
import { PageHeader } from '../components/Panel'
import { StatusBadge } from '../components/StatusBadge'
import { useAsync } from '../hooks/useAsync'
import { formatMoney } from '../lib/format'

// Adds up the summary rows that are in any of the given statuses.
function totalsFor(rows: ClaimsSummaryRow[], statuses: ClaimStatus[]) {
  const matching = rows.filter((row) => statuses.includes(row.status))
  return {
    count: matching.reduce((sum, row) => sum + row.claimCount, 0),
    amount: matching.reduce((sum, row) => sum + row.totalAmount, 0),
  }
}

interface StatTileProps {
  label: string
  value: string
  detail: string
}

function StatTile({ label, value, detail }: StatTileProps) {
  return (
    <div className="rounded-lg border border-line bg-white p-4">
      <dt className="text-xs font-semibold tracking-wide text-muted uppercase">{label}</dt>
      <dd className="mt-1 font-mono text-2xl font-medium">{value}</dd>
      <dd className="mt-0.5 text-xs text-muted">{detail}</dd>
    </div>
  )
}

const COLUMNS: Column<ClaimsSummaryRow>[] = [
  { header: 'Policy type', cell: (row) => row.policyType },
  { header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  { header: 'Claims', cell: (row) => row.claimCount, align: 'right' },
  { header: 'Total amount', cell: (row) => formatMoney(row.totalAmount), align: 'right' },
]

const claimWord = (count: number) => (count === 1 ? 'claim' : 'claims')

export function DashboardPage() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const load = useCallback(() => api.getClaimsSummary({ from, to }), [from, to])
  const { data: rows, error, reload } = useAsync(load)

  const all = totalsFor(rows ?? [], ['Submitted', 'UnderReview', 'Approved', 'Rejected', 'Paid'])
  const open = totalsFor(rows ?? [], ['Submitted', 'UnderReview'])
  const approved = totalsFor(rows ?? [], ['Approved'])
  const paid = totalsFor(rows ?? [], ['Paid'])

  return (
    <>
      <PageHeader title="Dashboard">Claims grouped by policy type and status.</PageHeader>

      <div className="mb-6 flex flex-wrap gap-4">
        <div className="w-44">
          <TextField label="From" type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div className="w-44">
          <TextField label="To" type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
        </div>
      </div>

      {error ? (
        <ErrorMessage message={error.message} onRetry={reload} />
      ) : !rows ? (
        <Loading label="Loading the claims summary…" />
      ) : rows.length === 0 ? (
        <EmptyState title="No claims in this period">
          Clear the dates to see every claim, or file one from the Claims page.
        </EmptyState>
      ) : (
        <>
          <dl className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile label="Claims filed" value={String(all.count)} detail={`${formatMoney(all.amount)} claimed`} />
            <StatTile
              label="Awaiting a decision"
              value={String(open.count)}
              detail={`${formatMoney(open.amount)} submitted or under review`}
            />
            <StatTile
              label="Approved, not yet paid"
              value={String(approved.count)}
              detail={`${formatMoney(approved.amount)} to pay`}
            />
            <StatTile
              label="Paid out"
              value={formatMoney(paid.amount)}
              detail={`${paid.count} ${claimWord(paid.count)} paid`}
            />
          </dl>
          <DataTable
            caption="Claims summary by policy type and status"
            columns={COLUMNS}
            rows={rows}
            rowKey={(row) => `${row.policyType}-${row.status}`}
          />
        </>
      )}
    </>
  )
}
