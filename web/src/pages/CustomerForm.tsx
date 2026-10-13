import { type SubmitEvent, useState } from 'react'
import { api, toApiError } from '../api/client'
import type { Customer } from '../api/types'
import { Button } from '../components/Button'
import { ErrorMessage, SuccessMessage } from '../components/Feedback'
import { TextField } from '../components/Field'

interface FormErrors {
  name?: string
  email?: string
}

function validate(name: string, email: string): FormErrors {
  const errors: FormErrors = {}
  if (name.trim().length < 2 || name.trim().length > 100) errors.name = 'Enter a name of 2 to 100 characters.'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) errors.email = 'Enter a valid email address.'
  return errors
}

export function CustomerForm({ onCreated }: { onCreated: (customer: Customer) => void }) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [errors, setErrors] = useState<FormErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [created, setCreated] = useState<Customer | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const found = validate(name, email)
    setErrors(found)
    setFormError(null)
    setCreated(null)
    if (found.name || found.email) return

    setBusy(true)
    try {
      const customer = await api.createCustomer({ name: name.trim(), email: email.trim() })
      setCreated(customer)
      setName('')
      setEmail('')
      onCreated(customer)
    } catch (err) {
      const error = toApiError(err)
      const fieldErrors = { name: error.fieldError('name'), email: error.fieldError('email') }
      setErrors(fieldErrors)
      setFormError(fieldErrors.name || fieldErrors.email ? null : error.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {formError && <ErrorMessage message={formError} />}
      {created && <SuccessMessage>{created.name} added.</SuccessMessage>}
      <TextField label="Customer name" value={name} error={errors.name} onChange={(e) => setName(e.target.value)} />
      <TextField
        label="Email"
        type="email"
        value={email}
        error={errors.email}
        onChange={(e) => setEmail(e.target.value)}
      />
      <Button type="submit" variant="secondary" disabled={busy}>
        Add customer
      </Button>
    </form>
  )
}
