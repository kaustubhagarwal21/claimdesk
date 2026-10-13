import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { setActingUserId } from '../api/session'
import type { User } from '../api/types'
import { UserContext } from '../auth/UserContext'

// The seeded users from docs/API.md.
export const asha: User = { id: 1, name: 'Asha', role: 'Agent' }
export const ravi: User = { id: 2, name: 'Ravi', role: 'Adjuster' }
export const meera: User = { id: 3, name: 'Meera', role: 'Adjuster' }
export const kiran: User = { id: 4, name: 'Kiran', role: 'Manager' }
export const allUsers = [asha, ravi, meera, kiran]

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

/** An RFC 7807 error response, shaped like the ones the API sends. */
export function problem(status: number, detail: string, errors?: Record<string, string[]>): Response {
  const body = { type: 'about:blank', title: 'Request failed', status, detail, errors }
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/problem+json' } })
}

export interface RecordedRequest {
  method: string
  url: string
  headers: Record<string, string>
  body: unknown
}

type RouteTable = Record<string, (body: unknown) => Response>

/**
 * Replaces fetch with a fake. Each route is keyed by "METHOD /api/path" and
 * returns the response to send. Every request made is recorded and returned,
 * so a test can check the headers and body that were sent.
 */
export function mockApi(routes: RouteTable): RecordedRequest[] {
  const requests: RecordedRequest[] = []

  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const method = init.method ?? 'GET'
    const url = String(input)
    const body: unknown = typeof init.body === 'string' ? JSON.parse(init.body) : undefined
    requests.push({ method, url, headers: init.headers as Record<string, string>, body })

    const route = routes[`${method} ${url}`]
    if (!route) throw new Error(`No mocked route for ${method} ${url}`)
    return route(body)
  })

  return requests
}

interface RenderOptions {
  /** Route pattern the component is mounted on, for example "/claims/:id". */
  path?: string
  /** The address the test starts at, for example "/claims/7". */
  route?: string
}

/** Renders a component inside a router, acting as the given user. */
export function renderAs(user: User, ui: ReactElement, { path = '/', route = '/' }: RenderOptions = {}) {
  setActingUserId(user.id)
  return render(
    <MemoryRouter initialEntries={[route]}>
      <UserContext.Provider value={{ users: allUsers, user, selectUser: () => {} }}>
        <Routes>
          <Route path={path} element={ui} />
        </Routes>
      </UserContext.Provider>
    </MemoryRouter>,
  )
}
