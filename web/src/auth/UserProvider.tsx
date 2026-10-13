import { type ReactNode, useEffect, useState } from 'react'
import { api, toApiError } from '../api/client'
import { getActingUserId, setActingUserId } from '../api/session'
import type { User } from '../api/types'
import { ErrorMessage, Loading } from '../components/Feedback'
import { UserContext } from './UserContext'

/**
 * Loads the users once and remembers which one we are acting as.
 * Pages are rendered only after a user is chosen, so every request they
 * make already carries the X-User-Id header.
 */
export function UserProvider({ children }: { children: ReactNode }) {
  const [users, setUsers] = useState<User[] | null>(null)
  const [userId, setUserId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false

    api
      .getUsers()
      .then((list) => {
        if (cancelled) return
        // Keep the stored choice if that user still exists, otherwise start as the first user.
        const stored = getActingUserId()
        const current = list.find((user) => user.id === stored) ?? list[0]
        if (!current) {
          setError('The API returned no users. Seed the database and try again.')
          return
        }
        setActingUserId(current.id)
        setUsers(list)
        setUserId(current.id)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(toApiError(err).message)
      })

    return () => {
      cancelled = true
    }
  }, [attempt])

  function selectUser(id: number) {
    setActingUserId(id)
    setUserId(id)
  }

  const user = users?.find((candidate) => candidate.id === userId)

  if (error) {
    return (
      <div className="mx-auto max-w-md px-4 py-16">
        <h1 className="mb-4 text-2xl">ClaimDesk</h1>
        <ErrorMessage message={error} onRetry={() => { setError(null); setAttempt(attempt + 1) }} />
      </div>
    )
  }

  if (!users || !user) return <Loading label="Loading users…" />

  return <UserContext.Provider value={{ users, user, selectUser }}>{children}</UserContext.Provider>
}
