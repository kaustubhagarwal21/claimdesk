import { getActingUserId } from './session'
import type {
  Claim,
  ClaimDetail,
  ClaimFilter,
  ClaimsSummaryRow,
  Customer,
  DateRange,
  NewClaim,
  NewCustomer,
  Policy,
  PolicyFilter,
  PolicyRequest,
  ProblemDetails,
  Quote,
  User,
} from './types'

export type FieldErrors = Record<string, string[]>

/** Every failed request is turned into one of these, so pages handle a single error type. */
export class ApiError extends Error {
  /** HTTP status, or 0 when the server could not be reached. */
  readonly status: number
  readonly title: string
  /** Validation messages by field name (camelCase). Empty unless the API sent `errors`. */
  readonly fieldErrors: FieldErrors

  constructor(status: number, title: string, message: string, fieldErrors: FieldErrors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.fieldErrors = fieldErrors
  }

  /** First validation message for a field, if there is one. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0]
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  const message = error instanceof Error ? error.message : 'Something went wrong.'
  return new ApiError(0, 'Unexpected error', message)
}

// Used when an error response has no problem-details body (for example, the API is down).
const FALLBACK_MESSAGES: Record<number, string> = {
  401: 'The API does not recognise the current user. Choose a user in the header.',
  403: 'Your role is not allowed to do this.',
  404: 'This record was not found.',
  409: 'This change conflicts with the current state of the record.',
}

// The server may name fields "SumInsured" or "$.sumInsured"; forms use "sumInsured".
function normaliseFieldErrors(errors: ProblemDetails['errors']): FieldErrors {
  const result: FieldErrors = {}
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const name = key.replace(/^\$\./, '')
    result[name.charAt(0).toLowerCase() + name.slice(1)] = messages
  }
  return result
}

async function readError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}
  try {
    const body: unknown = await response.json()
    if (typeof body === 'object' && body !== null) problem = body as ProblemDetails
  } catch {
    // The body was empty or not JSON; fall back to a message based on the status.
  }

  const { status } = response
  const fallback =
    FALLBACK_MESSAGES[status] ??
    (status >= 500
      ? `The API is not responding (status ${status}). Check that it is running, then try again.`
      : `The request failed with status ${status}.`)

  const title = problem.title ?? 'Request failed'
  const message = problem.detail ?? problem.title ?? fallback
  return new ApiError(status, title, message, normaliseFieldErrors(problem.errors))
}

async function request<T>(method: 'GET' | 'POST', path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const userId = getActingUserId()
  if (userId !== null) headers['X-User-Id'] = String(userId)
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Network error', 'Could not reach the server. Check that the API is running.')
  }

  if (!response.ok) throw await readError(response)
  return (await response.json()) as T
}

// Adds ?key=value for every filter that is set, and skips the empty ones.
function withQuery(path: string, params: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value))
  }
  const text = query.toString()
  return text ? `${path}?${text}` : path
}

export const api = {
  getUsers: () => request<User[]>('GET', '/users'),

  getCustomers: () => request<Customer[]>('GET', '/customers'),
  createCustomer: (customer: NewCustomer) => request<Customer>('POST', '/customers', customer),

  quotePolicy: (policy: PolicyRequest) => request<Quote>('POST', '/policies/quote', policy),
  issuePolicy: (policy: PolicyRequest) => request<Policy>('POST', '/policies', policy),
  getPolicies: (filter: PolicyFilter = {}) => request<Policy[]>('GET', withQuery('/policies', { ...filter })),
  getPolicy: (id: number) => request<Policy>('GET', `/policies/${id}`),
  cancelPolicy: (id: number) => request<Policy>('POST', `/policies/${id}/cancel`),

  fileClaim: (claim: NewClaim) => request<Claim>('POST', '/claims', claim),
  getClaims: (filter: ClaimFilter = {}) => request<Claim[]>('GET', withQuery('/claims', { ...filter })),
  getClaim: (id: number) => request<ClaimDetail>('GET', `/claims/${id}`),
  reviewClaim: (id: number) => request<Claim>('POST', `/claims/${id}/review`),
  approveClaim: (id: number, rowVersion: string) =>
    request<Claim>('POST', `/claims/${id}/approve`, { rowVersion }),
  rejectClaim: (id: number, reason: string, rowVersion: string) =>
    request<Claim>('POST', `/claims/${id}/reject`, { reason, rowVersion }),
  payClaim: (id: number, rowVersion: string) => request<Claim>('POST', `/claims/${id}/pay`, { rowVersion }),

  getClaimsSummary: (range: DateRange = {}) =>
    request<ClaimsSummaryRow[]>('GET', withQuery('/reports/claims-summary', { ...range })),
}
