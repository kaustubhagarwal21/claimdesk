import { createContext, useContext } from 'react'
import type { User } from '../api/types'

export interface UserContextValue {
  /** Everyone the demo can act as. */
  users: User[]
  /** The user the app is currently acting as. */
  user: User
  selectUser: (id: number) => void
}

export const UserContext = createContext<UserContextValue | null>(null)

export function useCurrentUser(): UserContextValue {
  const value = useContext(UserContext)
  if (!value) throw new Error('useCurrentUser must be used inside a UserProvider.')
  return value
}
