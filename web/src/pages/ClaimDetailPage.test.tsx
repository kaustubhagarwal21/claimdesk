import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { ClaimDetail, User } from '../api/types'
import { asha, json, kiran, meera, mockApi, problem, ravi, renderAs } from '../test/helpers'
import { ClaimDetailPage } from './ClaimDetailPage'

const submitted: ClaimDetail = {
  id: 7,
  claimNumber: 'CLM-0007',
  policyId: 3,
  policyNumber: 'POL-0003',
  policyType: 'Motor',
  incidentDate: '2026-09-20',
  amount: 45000,
  description: 'Rear bumper damaged in a parking accident.',
  status: 'Submitted',
  filedBy: 'Asha',
  reviewedBy: null,
  approvedBy: null,
  rejectionReason: null,
  createdAt: '2026-09-21T06:30:00Z',
  rowVersion: 'AAAAAAAAB9E=',
  audit: [{ oldStatus: null, newStatus: 'Submitted', changedBy: 'Asha', changedAt: '2026-09-21T06:30:00Z' }],
}

// The same claim after Ravi started the review.
const underReview: ClaimDetail = {
  ...submitted,
  status: 'UnderReview',
  reviewedBy: 'Ravi',
  rowVersion: 'AAAAAAAAB9I=',
  audit: [
    ...submitted.audit,
    { oldStatus: 'Submitted', newStatus: 'UnderReview', changedBy: 'Ravi', changedAt: '2026-09-22T04:10:00Z' },
  ],
}

const approved: ClaimDetail = { ...underReview, status: 'Approved', approvedBy: 'Meera', rowVersion: 'AAAAAAAAB9M=' }

function renderClaimPage(user: User) {
  return renderAs(user, <ClaimDetailPage />, { path: '/claims/:id', route: '/claims/7' })
}

const currentStep = () =>
  within(screen.getByRole('list', { name: 'Claim progress' }))
    .getAllByRole('listitem')
    .find((step) => step.getAttribute('aria-current') === 'step')

describe('ClaimDetailPage', () => {
  it('shows an agent the claim and its audit trail, but no action buttons', async () => {
    mockApi({ 'GET /api/claims/7': () => json(underReview) })
    renderClaimPage(asha)

    expect(await screen.findByRole('heading', { name: 'CLM-0007' })).toBeInTheDocument()
    expect(screen.getByText('₹45,000.00')).toBeInTheDocument()
    expect(currentStep()).toHaveTextContent('Under review')

    const audit = within(screen.getByRole('list', { name: 'Audit trail' })).getAllByRole('listitem')
    expect(audit).toHaveLength(2)
    expect(audit[0]).toHaveTextContent('Filed as Submitted')
    expect(audit[1]).toHaveTextContent('Submitted → Under review')
    expect(audit[1]).toHaveTextContent('Ravi')

    expect(screen.queryAllByRole('button')).toHaveLength(0)
    expect(screen.getByText('Waiting for a decision from an adjuster or a manager.')).toBeInTheDocument()
  })

  it('lets an adjuster start the review, then shows the reloaded claim', async () => {
    const user = userEvent.setup()
    let claim = submitted
    const requests = mockApi({
      'GET /api/claims/7': () => json(claim),
      'POST /api/claims/7/review': () => {
        claim = underReview
        return json(claim)
      },
    })
    renderClaimPage(ravi)

    await user.click(await screen.findByRole('button', { name: 'Start review' }))

    await waitFor(() => expect(currentStep()).toHaveTextContent('Under review'))
    expect(screen.getByRole('status')).toHaveTextContent('Review started.')
    expect(requests[1]).toMatchObject({ method: 'POST', url: '/api/claims/7/review', body: undefined })
    expect(requests[1].headers['X-User-Id']).toBe('2')
  })

  it('hides Approve from the adjuster who reviewed the claim, and shows it to another adjuster', async () => {
    mockApi({ 'GET /api/claims/7': () => json(underReview) })

    const reviewer = renderClaimPage(ravi)
    expect(await screen.findByRole('button', { name: 'Reject' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    expect(screen.getByText('You reviewed this claim, so someone else has to approve it.')).toBeInTheDocument()
    reviewer.unmount()

    renderClaimPage(meera)
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mark as paid' })).not.toBeInTheDocument()
  })

  it('asks for a reason before rejecting, then sends it with the rowVersion', async () => {
    const user = userEvent.setup()
    let claim = underReview
    const requests = mockApi({
      'GET /api/claims/7': () => json(claim),
      'POST /api/claims/7/reject': () => {
        claim = { ...underReview, status: 'Rejected', rejectionReason: 'Damage predates the policy.' }
        return json(claim)
      },
    })
    renderClaimPage(kiran)

    await user.click(await screen.findByRole('button', { name: 'Reject' }))
    const reason = screen.getByLabelText('Reason for rejection')
    await user.type(reason, 'No')
    await user.click(screen.getByRole('button', { name: 'Confirm rejection' }))

    expect(reason).toHaveAccessibleDescription('Give a reason of 5 to 300 characters.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)

    await user.clear(reason)
    await user.type(reason, 'Damage predates the policy.')
    await user.click(screen.getByRole('button', { name: 'Confirm rejection' }))

    await waitFor(() => expect(currentStep()).toHaveTextContent('Rejected'))
    expect(requests.find((request) => request.method === 'POST')?.body).toEqual({
      reason: 'Damage predates the policy.',
      rowVersion: 'AAAAAAAAB9I=',
    })
    expect(screen.getByText('Damage predates the policy.')).toBeInTheDocument()
  })

  it('after a 409 shows the server message, reloads the claim and uses the new rowVersion', async () => {
    const user = userEvent.setup()
    let claim = underReview
    const requests = mockApi({
      'GET /api/claims/7': () => json(claim),
      // Meera approved the claim in another window, so the rowVersion this page holds is stale.
      'POST /api/claims/7/approve': () => {
        claim = approved
        return problem(409, 'The claim was changed by someone else.')
      },
      'POST /api/claims/7/pay': () => {
        claim = { ...approved, status: 'Paid' }
        return json(claim)
      },
    })
    renderClaimPage(kiran)

    await user.click(await screen.findByRole('button', { name: 'Approve' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The claim was changed by someone else. The claim has been reloaded with its latest details.',
    )
    expect(requests.find((request) => request.url.endsWith('/approve'))?.body).toEqual({ rowVersion: 'AAAAAAAAB9I=' })

    // The reload replaced the stale claim: the page now shows the approved claim and its next action.
    const payButton = await screen.findByRole('button', { name: 'Mark as paid' })
    expect(currentStep()).toHaveTextContent('Approved')
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    expect(requests.filter((request) => request.method === 'GET')).toHaveLength(2)

    await user.click(payButton)

    await waitFor(() => expect(currentStep()).toHaveTextContent('Paid'))
    expect(requests.find((request) => request.url.endsWith('/pay'))?.body).toEqual({ rowVersion: 'AAAAAAAAB9M=' })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows the server message after a 403 and keeps the claim on screen', async () => {
    const user = userEvent.setup()
    const requests = mockApi({
      'GET /api/claims/7': () => json(approved),
      'POST /api/claims/7/pay': () => problem(403, 'Only a Manager can pay a claim.'),
    })
    renderClaimPage(kiran)

    await user.click(await screen.findByRole('button', { name: 'Mark as paid' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Only a Manager can pay a claim.')
    expect(screen.getByRole('heading', { name: 'CLM-0007' })).toBeInTheDocument()
    expect(currentStep()).toHaveTextContent('Approved')
    // Nothing changed on the server, so there is no second GET.
    expect(requests.filter((request) => request.method === 'GET')).toHaveLength(1)
  })

  it('shows an error state when the claim does not exist', async () => {
    mockApi({ 'GET /api/claims/7': () => problem(404, 'Claim 7 was not found.') })
    renderClaimPage(asha)

    expect(await screen.findByRole('alert')).toHaveTextContent('This claim does not exist.')
    expect(screen.getByRole('link', { name: 'Back to all claims' })).toHaveAttribute('href', '/claims')
  })
})
