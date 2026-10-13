import { Link, NavLink, Outlet } from 'react-router-dom'
import { UserSwitcher } from './UserSwitcher'

const NAV_LINKS = [
  { to: '/', label: 'Dashboard' },
  { to: '/policies', label: 'Policies' },
  { to: '/claims', label: 'Claims' },
]

/** The header shared by every page. <Outlet /> is where the current page renders. */
export function Layout() {
  return (
    <div className="min-h-screen">
      <header className="border-b-4 border-marigold bg-ink text-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-8 gap-y-3 px-4 py-3">
          <Link to="/" className="text-lg font-extrabold tracking-tight [font-stretch:120%]">
            ClaimDesk
          </Link>
          <nav aria-label="Main" className="flex gap-1">
            {NAV_LINKS.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.to === '/'}
                className={({ isActive }) =>
                  `rounded-md px-3 py-1.5 text-sm font-medium ${
                    isActive ? 'bg-white text-ink' : 'text-white/80 hover:bg-white/10 hover:text-white'
                  }`
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
          <div className="sm:ml-auto">
            <UserSwitcher />
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
