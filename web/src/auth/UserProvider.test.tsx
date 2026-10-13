import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { api } from '../api/client'
import { getActingUserId } from '../api/session'
import { UserSwitcher } from '../components/UserSwitcher'
import { allUsers, json, mockApi } from '../test/helpers'
import { useCurrentUser } from './UserContext'
import { UserProvider } from './UserProvider'

function CurrentRole() {
  const { user } = useCurrentUser()
  return <p>Role: {user.role}</p>
}

describe('UserProvider and UserSwitcher', () => {
  it('starts as the first user, and switching changes the role and the X-User-Id that is sent', async () => {
    const user = userEvent.setup()
    const requests = mockApi({
      'GET /api/users': () => json(allUsers),
      'GET /api/customers': () => json([]),
    })
    render(
      <UserProvider>
        <UserSwitcher />
        <CurrentRole />
      </UserProvider>,
    )

    const switcher = await screen.findByLabelText('Acting as')
    expect(switcher).toHaveValue('1')
    expect(screen.getByText('Role: Agent')).toBeInTheDocument()
    expect(getActingUserId()).toBe(1)

    await user.selectOptions(switcher, 'Kiran (Manager)')

    expect(screen.getByText('Role: Manager')).toBeInTheDocument()
    expect(getActingUserId()).toBe(4)
    await api.getCustomers()
    expect(requests.at(-1)?.headers['X-User-Id']).toBe('4')
  })
})
