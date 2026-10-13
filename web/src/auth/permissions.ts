import type { Claim, Role, User } from '../api/types'

// These rules mirror the "who" column and the business rules in docs/API.md.
// They only decide what the UI offers; the server still enforces every rule.

export const MANAGER_APPROVAL_LIMIT = 100_000

export function canManagePolicies(role: Role): boolean {
  return role === 'Agent' || role === 'Manager'
}

export function canFileClaim(role: Role): boolean {
  return role === 'Agent'
}

export type ClaimAction = 'review' | 'approve' | 'reject' | 'pay'

export interface ClaimActions {
  actions: ClaimAction[]
  /** Explains why nothing, or not everything, is on offer. */
  note: string | null
}

export function getClaimActions(user: User, claim: Claim): ClaimActions {
  const { role } = user

  switch (claim.status) {
    case 'Submitted':
      return role === 'Adjuster'
        ? { actions: ['review'], note: null }
        : { actions: [], note: 'Waiting for an adjuster to start the review.' }

    case 'UnderReview':
      if (role === 'Agent') {
        return { actions: [], note: 'Waiting for a decision from an adjuster or a manager.' }
      }
      // Maker-checker: the person who reviewed a claim cannot also approve it.
      if (claim.reviewedBy === user.name) {
        return { actions: ['reject'], note: 'You reviewed this claim, so someone else has to approve it.' }
      }
      if (role === 'Adjuster' && claim.amount > MANAGER_APPROVAL_LIMIT) {
        return { actions: ['reject'], note: 'A claim above ₹1,00,000 can be approved only by a manager.' }
      }
      return { actions: ['approve', 'reject'], note: null }

    case 'Approved':
      return role === 'Manager'
        ? { actions: ['pay'], note: null }
        : { actions: [], note: 'Waiting for a manager to record the payment.' }

    default:
      return { actions: [], note: 'This claim is closed. No further action is possible.' }
  }
}
