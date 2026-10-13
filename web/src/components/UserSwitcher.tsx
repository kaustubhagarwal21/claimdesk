import { useCurrentUser } from '../auth/UserContext'

/** The demo has no login: this select decides whose X-User-Id is sent with each request. */
export function UserSwitcher() {
  const { users, user, selectUser } = useCurrentUser()

  return (
    <div className="flex items-center gap-2 text-sm">
      <label htmlFor="acting-as" className="text-white/70">
        Acting as
      </label>
      <select
        id="acting-as"
        value={user.id}
        onChange={(event) => selectUser(Number(event.target.value))}
        className="rounded-md border border-white/30 bg-ink px-2 py-1.5 font-medium text-white"
      >
        {users.map((option) => (
          <option key={option.id} value={option.id}>
            {option.name} ({option.role})
          </option>
        ))}
      </select>
    </div>
  )
}
