// Types for every shape in docs/API.md. Dates are "yyyy-MM-dd", timestamps are ISO 8601 UTC.

export type Role = 'Agent' | 'Adjuster' | 'Manager'
export type PolicyType = 'Motor' | 'Health' | 'Home'
export type PolicyStatus = 'Active' | 'Cancelled' | 'Expired'
export type ClaimStatus = 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Paid'

export const POLICY_TYPES: PolicyType[] = ['Motor', 'Health', 'Home']
export const POLICY_STATUSES: PolicyStatus[] = ['Active', 'Cancelled', 'Expired']
export const CLAIM_STATUSES: ClaimStatus[] = ['Submitted', 'UnderReview', 'Approved', 'Rejected', 'Paid']

export interface User {
  id: number
  name: string
  role: Role
}

export interface Customer {
  id: number
  name: string
  email: string
}

export interface Quote {
  type: PolicyType
  sumInsured: number
  ratePercent: number
  premium: number
}

export interface Policy {
  id: number
  policyNumber: string
  customerId: number
  customerName: string
  type: PolicyType
  sumInsured: number
  premium: number
  startDate: string
  endDate: string
  status: PolicyStatus
  remainingCover: number
}

export interface Claim {
  id: number
  claimNumber: string
  policyId: number
  policyNumber: string
  policyType: PolicyType
  incidentDate: string
  amount: number
  description: string
  status: ClaimStatus
  filedBy: string | null
  reviewedBy: string | null
  approvedBy: string | null
  rejectionReason: string | null
  createdAt: string
  /** Base64 concurrency token. Sent back with approve, reject and pay. */
  rowVersion: string
}

export interface AuditEntry {
  /** null for the first entry, when the claim was filed. */
  oldStatus: ClaimStatus | null
  newStatus: ClaimStatus
  changedBy: string | null
  changedAt: string
}

export interface ClaimDetail extends Claim {
  audit: AuditEntry[]
}

export interface ClaimsSummaryRow {
  policyType: PolicyType
  status: ClaimStatus
  claimCount: number
  totalAmount: number
}

// Request bodies

export interface NewCustomer {
  name: string
  email: string
}

/** Body of both POST /policies/quote and POST /policies. */
export interface PolicyRequest {
  customerId: number
  type: PolicyType
  sumInsured: number
  startDate: string
}

export interface NewClaim {
  policyId: number
  incidentDate: string
  amount: number
  description: string
}

// Query filters

export interface PolicyFilter {
  status?: PolicyStatus
  customerId?: number
}

export interface ClaimFilter {
  status?: ClaimStatus
  policyId?: number
}

export interface DateRange {
  from?: string
  to?: string
}

/** RFC 7807 error body returned by the API. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
