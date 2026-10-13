import { Link } from 'react-router-dom'
import { PageHeader } from '../components/Panel'

export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found">There is nothing at this address.</PageHeader>
      <Link to="/" className="font-semibold text-brand underline">
        Go to the dashboard
      </Link>
    </>
  )
}
