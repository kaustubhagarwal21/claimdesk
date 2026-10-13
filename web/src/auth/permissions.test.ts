import { describe, expect, it } from 'vitest'
import type { Claim, ClaimStatus } from '../api/types'
import { asha, kiran, meera, ravi } from '../test/helpers'
import { getClaimActions } from './permissions'

function claimWith(status: ClaimStatus, overrides: Partial<Claim> = {}): Claim {
  return {
    id: 7,
    claimNumber: 'CLM-0007',
    policyId: 3,
    policyNumber: 'POL-0003',
    policyType: 'Motor',
    incidentDate: '2026-09-20',
    amount: 45000,
    description: 'Rear bumper damaged in a parking accident.',
    status,
    filedBy: 'Asha',
    reviewedBy: status === 'Submitted' ? null : 'Ravi',
    approvedBy: null,
    rejectionReason: null,
    createdAt: '2026-09-21T06:30:00Z',
    rowVersion: 'AAAAAAAAB9E=',
    ...overrides,
  }
}

describe('getClaimActions', () => {
  it('offers an agent no actions at any stage, and nobody any action on a closed claim', () => {
    for (const status of ['Submitted', 'UnderReview', 'Approved', 'Rejected', 'Paid'] as const) {
      expect(getClaimActions(asha, claimWith(status)).actions).toEqual([])
    }
    expect(getClaimActions(kiran, claimWith('Paid')).actions).toEqual([])
    expect(getClaimActions(ravi, claimWith('Rejected')).actions).toEqual([])
  })

  it('lets only an adjuster start the review of a submitted claim', () => {
    expect(getClaimActions(ravi, claimWith('Submitted')).actions).toEqual(['review'])
    expect(getClaimActions(kiran, claimWith('Submitted')).actions).toEqual([])
  })

  it('applies maker-checker: the reviewer can reject but not approve', () => {
    const claim = claimWith('UnderReview', { reviewedBy: 'Ravi' })

    expect(getClaimActions(ravi, claim).actions).toEqual(['reject'])
    expect(getClaimActions(ravi, claim).note).toMatch(/someone else has to approve/)
    expect(getClaimActions(meera, claim).actions).toEqual(['approve', 'reject'])
    expect(getClaimActions(kiran, claim).actions).toEqual(['approve', 'reject'])
  })

  it('leaves approval of a claim above 1,00,000 to a manager', () => {
    const atLimit = claimWith('UnderReview', { amount: 100000 })
    const aboveLimit = claimWith('UnderReview', { amount: 100000.01 })

    expect(getClaimActions(meera, atLimit).actions).toEqual(['approve', 'reject'])
    expect(getClaimActions(meera, aboveLimit).actions).toEqual(['reject'])
    expect(getClaimActions(kiran, aboveLimit).actions).toEqual(['approve', 'reject'])
  })

  it('lets only a manager pay an approved claim', () => {
    expect(getClaimActions(kiran, claimWith('Approved')).actions).toEqual(['pay'])
    expect(getClaimActions(meera, claimWith('Approved')).actions).toEqual([])
  })
})
