import { describe, expect, it, vi } from 'vitest'
import { json, mockApi, problem } from '../test/helpers'
import { ApiError, api } from './client'
import { setActingUserId } from './session'

describe('request headers and bodies', () => {
  it('sends the acting user id in the X-User-Id header', async () => {
    setActingUserId(4)
    const requests = mockApi({ 'GET /api/customers': () => json([]) })

    await api.getCustomers()

    expect(requests[0].headers['X-User-Id']).toBe('4')
  })

  it('sends no X-User-Id header before a user has been chosen', async () => {
    const requests = mockApi({ 'GET /api/users': () => json([]) })

    await api.getUsers()

    expect(requests[0].headers).not.toHaveProperty('X-User-Id')
  })

  it('posts the body as JSON and returns the parsed response', async () => {
    setActingUserId(4)
    const approved = { id: 7, status: 'Approved' }
    const requests = mockApi({ 'POST /api/claims/7/approve': () => json(approved) })

    const result = await api.approveClaim(7, 'AAAAAAAAB9E=')

    expect(result).toEqual(approved)
    expect(requests[0].headers['Content-Type']).toBe('application/json')
    expect(requests[0].body).toEqual({ rowVersion: 'AAAAAAAAB9E=' })
  })

  it('adds only the filters that are set to the query string', async () => {
    const requests = mockApi({
      'GET /api/claims?status=Approved&policyId=3': () => json([]),
      'GET /api/policies': () => json([]),
      'GET /api/reports/claims-summary?to=2026-01-31': () => json([]),
    })

    await api.getClaims({ status: 'Approved', policyId: 3 })
    await api.getPolicies({ status: undefined })
    await api.getClaimsSummary({ from: '', to: '2026-01-31' })

    expect(requests.map((request) => request.url)).toEqual([
      '/api/claims?status=Approved&policyId=3',
      '/api/policies',
      '/api/reports/claims-summary?to=2026-01-31',
    ])
  })
})

describe('error parsing', () => {
  it('turns a problem-details response into an ApiError with the server message', async () => {
    mockApi({ 'POST /api/claims/7/pay': () => problem(409, 'The claim was changed by someone else.') })

    const error = await api.payClaim(7, 'stale').catch((err: unknown) => err)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 409,
      title: 'Request failed',
      message: 'The claim was changed by someone else.',
      fieldErrors: {},
    })
  })

  it('collects validation errors under camelCase field names', async () => {
    mockApi({
      'POST /api/policies/quote': () =>
        problem(400, 'One or more validation errors occurred.', {
          SumInsured: ['Sum insured must be between 10000 and 10000000.'],
          '$.startDate': ['The start date is not a valid date.', 'A second message.'],
        }),
    })

    const error = await api
      .quotePolicy({ customerId: 1, type: 'Motor', sumInsured: 5, startDate: 'x' })
      .catch((err: unknown) => err as ApiError)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.fieldError('sumInsured')).toBe('Sum insured must be between 10000 and 10000000.')
    expect(apiError.fieldError('startDate')).toBe('The start date is not a valid date.')
    expect(apiError.fieldError('customerId')).toBeUndefined()
  })

  it('falls back to a message for the status when the error body is not JSON', async () => {
    mockApi({ 'POST /api/claims/7/review': () => new Response('<h1>Forbidden</h1>', { status: 403 }) })

    await expect(api.reviewClaim(7)).rejects.toMatchObject({
      status: 403,
      message: 'Your role is not allowed to do this.',
    })
  })

  it('reports a network failure as an ApiError with status 0', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Failed to fetch')))

    await expect(api.getCustomers()).rejects.toMatchObject({
      name: 'ApiError',
      status: 0,
      message: 'Could not reach the server. Check that the API is running.',
    })
  })
})
