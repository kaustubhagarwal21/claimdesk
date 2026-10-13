import { type SubmitEvent, useCallback, useState } from 'react'
import { api, toApiError } from '../api/client'
import type { Claim, Policy } from '../api/types'
import { Button } from '../components/Button'
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback'
import { SelectField, TextAreaField, TextField } from '../components/Field'
import { useAsync } from '../hooks/useAsync'
import { formatDate, formatMoney } from '../lib/format'

interface FormValues {
  policyId: string
  incidentDate: string
  amount: string
  description: string
}

type FormErrors = Partial<Record<keyof FormValues, string>>

const EMPTY_VALUES: FormValues = { policyId: '', incidentDate: '', amount: '', description: '' }

// The same checks the API makes, done first in the browser so the user gets an instant answer.
function validate(values: FormValues, policy: Policy | undefined): FormErrors {
  const errors: FormErrors = {}
  const amount = Number(values.amount)
  const description = values.description.trim()

  if (!policy) errors.policyId = 'Choose a policy.'

  if (!values.incidentDate) {
    errors.incidentDate = 'Choose the incident date.'
  } else if (policy && (values.incidentDate < policy.startDate || values.incidentDate > policy.endDate)) {
    // ISO dates sort like text, so they can be compared as strings.
    errors.incidentDate = `The incident must fall inside the policy term, ${formatDate(policy.startDate)} to ${formatDate(policy.endDate)}.`
  }

  if (values.amount.trim() === '' || Number.isNaN(amount) || amount <= 0) {
    errors.amount = 'Enter an amount greater than 0.'
  } else if (policy && amount > policy.remainingCover) {
    errors.amount = `The amount cannot be more than the remaining cover of ${formatMoney(policy.remainingCover)}.`
  }

  if (description.length < 5 || description.length > 500) {
    errors.description = 'Describe what happened in 5 to 500 characters.'
  }

  return errors
}

/** Files a claim on an active policy. Shown to agents only. */
export function ClaimForm({ onFiled }: { onFiled: (claim: Claim) => void }) {
  const loadPolicies = useCallback(() => api.getPolicies({ status: 'Active' }), [])
  const { data: policies, error: loadError, reload } = useAsync(loadPolicies)

  const [values, setValues] = useState<FormValues>(EMPTY_VALUES)
  const [errors, setErrors] = useState<FormErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (loadError) return <ErrorMessage message={loadError.message} onRetry={reload} />
  if (!policies) return <Loading label="Loading active policies…" />
  if (policies.length === 0) {
    return <EmptyState title="No active policies">A claim can be filed only on an active policy.</EmptyState>
  }

  const policy = policies.find((candidate) => String(candidate.id) === values.policyId)

  function change(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
  }

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const found = validate(values, policy)
    setErrors(found)
    setFormError(null)
    if (!policy || Object.keys(found).length > 0) return

    setBusy(true)
    try {
      const claim = await api.fileClaim({
        policyId: policy.id,
        incidentDate: values.incidentDate,
        amount: Number(values.amount),
        description: values.description.trim(),
      })
      setValues(EMPTY_VALUES)
      onFiled(claim)
    } catch (err) {
      const error = toApiError(err)
      const fieldErrors: FormErrors = {
        policyId: error.fieldError('policyId'),
        incidentDate: error.fieldError('incidentDate'),
        amount: error.fieldError('amount'),
        description: error.fieldError('description'),
      }
      setErrors(fieldErrors)
      setFormError(Object.values(fieldErrors).some(Boolean) ? null : error.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {formError && <ErrorMessage message={formError} />}

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <SelectField
            label="Policy"
            value={values.policyId}
            error={errors.policyId}
            hint={
              policy
                ? `Term ${formatDate(policy.startDate)} to ${formatDate(policy.endDate)}. Remaining cover ${formatMoney(policy.remainingCover)}.`
                : undefined
            }
            onChange={(e) => change('policyId', e.target.value)}
          >
            <option value="">Choose an active policy</option>
            {policies.map((option) => (
              <option key={option.id} value={option.id}>
                {option.policyNumber} · {option.customerName} · {option.type}
              </option>
            ))}
          </SelectField>
        </div>

        <TextField
          label="Incident date"
          type="date"
          min={policy?.startDate}
          max={policy?.endDate}
          value={values.incidentDate}
          error={errors.incidentDate}
          onChange={(e) => change('incidentDate', e.target.value)}
        />

        <TextField
          label="Claim amount (₹)"
          type="number"
          inputMode="decimal"
          min={0}
          step="0.01"
          value={values.amount}
          error={errors.amount}
          onChange={(e) => change('amount', e.target.value)}
        />

        <div className="sm:col-span-2">
          <TextAreaField
            label="What happened"
            maxLength={500}
            value={values.description}
            error={errors.description}
            onChange={(e) => change('description', e.target.value)}
          />
        </div>
      </div>

      <Button type="submit" disabled={busy}>
        File claim
      </Button>
    </form>
  )
}
