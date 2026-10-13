import { type SubmitEvent, useState } from 'react'
import { api, toApiError } from '../api/client'
import { type Customer, POLICY_TYPES, type Policy, type PolicyRequest, type PolicyType, type Quote } from '../api/types'
import { useCurrentUser } from '../auth/UserContext'
import { canManagePolicies } from '../auth/permissions'
import { Button } from '../components/Button'
import { ErrorMessage, SuccessMessage } from '../components/Feedback'
import { SelectField, TextField } from '../components/Field'
import { formatMoney } from '../lib/format'

const MIN_SUM_INSURED = 10_000
const MAX_SUM_INSURED = 10_000_000

// Inputs hold text, so the form keeps strings and converts them when it sends the request.
interface FormValues {
  customerId: string
  type: PolicyType
  sumInsured: string
  startDate: string
}

type FormErrors = Partial<Record<keyof FormValues, string>>

const EMPTY_VALUES: FormValues = { customerId: '', type: 'Motor', sumInsured: '', startDate: '' }

function validate(values: FormValues): FormErrors {
  const errors: FormErrors = {}
  const sumInsured = Number(values.sumInsured)

  if (!values.customerId) errors.customerId = 'Choose a customer.'
  if (values.sumInsured.trim() === '' || Number.isNaN(sumInsured)) {
    errors.sumInsured = 'Enter the sum insured.'
  } else if (sumInsured < MIN_SUM_INSURED || sumInsured > MAX_SUM_INSURED) {
    errors.sumInsured = `The sum insured must be between ${formatMoney(MIN_SUM_INSURED)} and ${formatMoney(MAX_SUM_INSURED)}.`
  }
  if (!values.startDate) errors.startDate = 'Choose a start date.'

  return errors
}

interface PolicyFormProps {
  customers: Customer[]
  onIssued: (policy: Policy) => void
}

/** Step 1: get a quote (nothing is saved). Step 2: issue the policy from that quote. */
export function PolicyForm({ customers, onIssued }: PolicyFormProps) {
  const { user } = useCurrentUser()
  const mayIssue = canManagePolicies(user.role)

  const [values, setValues] = useState<FormValues>(EMPTY_VALUES)
  const [errors, setErrors] = useState<FormErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  // The request is stored next to its quote, so the policy issued is exactly the one quoted.
  const [quote, setQuote] = useState<{ request: PolicyRequest; result: Quote } | null>(null)
  const [issued, setIssued] = useState<Policy | null>(null)
  const [busy, setBusy] = useState(false)

  function change(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
    setQuote(null) // a quote is only valid for the values it was made from
    setIssued(null)
  }

  // Shows a server error next to its field when the API names one, otherwise above the form.
  function showFailure(err: unknown) {
    const error = toApiError(err)
    const fieldErrors: FormErrors = {
      customerId: error.fieldError('customerId'),
      type: error.fieldError('type'),
      sumInsured: error.fieldError('sumInsured'),
      startDate: error.fieldError('startDate'),
    }
    const hasFieldError = Object.values(fieldErrors).some(Boolean)
    setErrors(fieldErrors)
    setFormError(hasFieldError ? null : error.message)
  }

  async function getQuote(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const found = validate(values)
    setErrors(found)
    setFormError(null)
    setIssued(null)
    if (Object.keys(found).length > 0) return

    const request: PolicyRequest = {
      customerId: Number(values.customerId),
      type: values.type,
      sumInsured: Number(values.sumInsured),
      startDate: values.startDate,
    }
    setBusy(true)
    try {
      setQuote({ request, result: await api.quotePolicy(request) })
    } catch (err) {
      showFailure(err)
    } finally {
      setBusy(false)
    }
  }

  async function issuePolicy() {
    if (!quote) return
    setBusy(true)
    setFormError(null)
    try {
      const policy = await api.issuePolicy(quote.request)
      setIssued(policy)
      setQuote(null)
      setValues(EMPTY_VALUES)
      onIssued(policy)
    } catch (err) {
      showFailure(err)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={getQuote} noValidate className="space-y-4">
      {formError && <ErrorMessage message={formError} />}
      {issued && (
        <SuccessMessage>
          Policy {issued.policyNumber} issued for {issued.customerName}.
        </SuccessMessage>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <SelectField
          label="Customer"
          value={values.customerId}
          error={errors.customerId}
          hint={customers.length === 0 ? 'There are no customers yet. Add one first.' : undefined}
          onChange={(e) => change('customerId', e.target.value)}
        >
          <option value="">Choose a customer</option>
          {customers.map((customer) => (
            <option key={customer.id} value={customer.id}>
              {customer.name}
            </option>
          ))}
        </SelectField>

        <SelectField
          label="Policy type"
          value={values.type}
          error={errors.type}
          onChange={(e) => change('type', e.target.value)}
        >
          {POLICY_TYPES.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </SelectField>

        <TextField
          label="Sum insured (₹)"
          type="number"
          inputMode="numeric"
          min={MIN_SUM_INSURED}
          max={MAX_SUM_INSURED}
          value={values.sumInsured}
          error={errors.sumInsured}
          hint={`${formatMoney(MIN_SUM_INSURED)} to ${formatMoney(MAX_SUM_INSURED)}`}
          onChange={(e) => change('sumInsured', e.target.value)}
        />

        <TextField
          label="Start date"
          type="date"
          value={values.startDate}
          error={errors.startDate}
          hint="The policy runs for one year from this date."
          onChange={(e) => change('startDate', e.target.value)}
        />
      </div>

      <Button type="submit" variant={quote ? 'secondary' : 'primary'} disabled={busy}>
        Get quote
      </Button>

      {quote && (
        <div role="group" aria-label="Quote" className="rounded-md border border-brand/30 bg-brand/5 p-4">
          <p className="text-xs font-semibold tracking-wide text-muted uppercase">Yearly premium</p>
          <p className="font-mono text-2xl font-medium">{formatMoney(quote.result.premium)}</p>
          <p className="mt-1 text-sm text-muted">
            {quote.result.ratePercent}% of {formatMoney(quote.result.sumInsured)} for {quote.result.type} cover.
          </p>
          {mayIssue ? (
            <Button className="mt-3" onClick={issuePolicy} disabled={busy}>
              Issue policy
            </Button>
          ) : (
            <p className="mt-3 text-sm">Only an agent or a manager can issue this policy.</p>
          )}
        </div>
      )}
    </form>
  )
}
