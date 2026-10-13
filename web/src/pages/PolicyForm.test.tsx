import { screen, within } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { Customer, Policy, Quote } from '../api/types'
import { asha, json, mockApi, problem, ravi, renderAs } from '../test/helpers'
import { PolicyForm } from './PolicyForm'

const customers: Customer[] = [
  { id: 11, name: 'Arjun Mehta', email: 'arjun@example.com' },
  { id: 12, name: 'Nisha Rao', email: 'nisha@example.com' },
]

const quote: Quote = { type: 'Health', sumInsured: 500000, ratePercent: 4, premium: 20000 }

const policy: Policy = {
  id: 42,
  policyNumber: 'POL-0042',
  customerId: 12,
  customerName: 'Nisha Rao',
  type: 'Health',
  sumInsured: 500000,
  premium: 20000,
  startDate: '2026-11-01',
  endDate: '2027-10-31',
  status: 'Active',
  remainingCover: 500000,
}

const expectedRequest = { customerId: 12, type: 'Health', sumInsured: 500000, startDate: '2026-11-01' }

async function fillForm(user: UserEvent, sumInsured = '500000') {
  await user.selectOptions(screen.getByLabelText('Customer'), 'Nisha Rao')
  await user.selectOptions(screen.getByLabelText('Policy type'), 'Health')
  await user.type(screen.getByLabelText('Sum insured (₹)'), sumInsured)
  await user.type(screen.getByLabelText('Start date'), '2026-11-01')
}

const getQuoteButton = () => screen.getByRole('button', { name: 'Get quote' })

describe('PolicyForm validation', () => {
  it('shows an error under each empty field and sends nothing', async () => {
    const user = userEvent.setup()
    const requests = mockApi({})
    renderAs(asha, <PolicyForm customers={customers} onIssued={vi.fn()} />)

    await user.click(getQuoteButton())

    expect(screen.getByLabelText('Customer')).toBeInvalid()
    expect(screen.getByLabelText('Customer')).toHaveAccessibleDescription('Choose a customer.')
    expect(screen.getByLabelText('Sum insured (₹)')).toHaveAccessibleDescription('Enter the sum insured.')
    expect(screen.getByLabelText('Start date')).toHaveAccessibleDescription('Choose a start date.')
    expect(requests).toHaveLength(0)
  })

  it('rejects a sum insured below 10,000 or above 1,00,00,000', async () => {
    const user = userEvent.setup()
    const requests = mockApi({})
    renderAs(asha, <PolicyForm customers={customers} onIssued={vi.fn()} />)
    const sumInsured = screen.getByLabelText('Sum insured (₹)')
    const rangeMessage = 'The sum insured must be between ₹10,000.00 and ₹1,00,00,000.00.'

    await fillForm(user, '9999')
    await user.click(getQuoteButton())
    expect(sumInsured).toHaveAccessibleDescription(rangeMessage)

    await user.clear(sumInsured)
    await user.type(sumInsured, '10000001')
    // Editing the field clears its error (the hint is back) until the form is submitted again.
    expect(sumInsured).toHaveAccessibleDescription('₹10,000.00 to ₹1,00,00,000.00')
    await user.click(getQuoteButton())
    expect(sumInsured).toHaveAccessibleDescription(rangeMessage)

    expect(requests).toHaveLength(0)
  })

  it('shows a validation error from the server next to its field', async () => {
    const user = userEvent.setup()
    mockApi({
      'POST /api/policies/quote': () =>
        problem(400, 'One or more validation errors occurred.', { StartDate: ['The start date is too far in the past.'] }),
    })
    renderAs(asha, <PolicyForm customers={customers} onIssued={vi.fn()} />)

    await fillForm(user)
    await user.click(getQuoteButton())

    const startDate = await screen.findByLabelText('Start date')
    await vi.waitFor(() => expect(startDate).toHaveAccessibleDescription('The start date is too far in the past.'))
    expect(screen.queryByRole('group', { name: 'Quote' })).not.toBeInTheDocument()
  })
})

describe('PolicyForm quote and issue', () => {
  it('gets a quote and then issues the policy with the quoted values', async () => {
    const user = userEvent.setup()
    const onIssued = vi.fn()
    const requests = mockApi({
      'POST /api/policies/quote': () => json(quote),
      'POST /api/policies': () => json(policy, 201),
    })
    renderAs(asha, <PolicyForm customers={customers} onIssued={onIssued} />)

    await fillForm(user)
    await user.click(getQuoteButton())

    const quoteBox = await screen.findByRole('group', { name: 'Quote' })
    expect(quoteBox).toHaveTextContent('₹20,000.00')
    expect(quoteBox).toHaveTextContent('4% of ₹5,00,000.00 for Health cover.')
    expect(requests[0]).toMatchObject({ method: 'POST', url: '/api/policies/quote', body: expectedRequest })

    await user.click(within(quoteBox).getByRole('button', { name: 'Issue policy' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Policy POL-0042 issued for Nisha Rao.')
    expect(requests[1]).toMatchObject({ method: 'POST', url: '/api/policies', body: expectedRequest })
    expect(onIssued).toHaveBeenCalledWith(policy)
  })

  it('drops the quote when a value changes, so a stale quote cannot be issued', async () => {
    const user = userEvent.setup()
    mockApi({ 'POST /api/policies/quote': () => json(quote) })
    renderAs(asha, <PolicyForm customers={customers} onIssued={vi.fn()} />)

    await fillForm(user)
    await user.click(getQuoteButton())
    expect(await screen.findByRole('group', { name: 'Quote' })).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Policy type'), 'Home')

    expect(screen.queryByRole('group', { name: 'Quote' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Issue policy' })).not.toBeInTheDocument()
  })

  it('lets an adjuster get a quote but offers no way to issue the policy', async () => {
    const user = userEvent.setup()
    mockApi({ 'POST /api/policies/quote': () => json(quote) })
    renderAs(ravi, <PolicyForm customers={customers} onIssued={vi.fn()} />)

    await fillForm(user)
    await user.click(getQuoteButton())

    const quoteBox = await screen.findByRole('group', { name: 'Quote' })
    expect(quoteBox).toHaveTextContent('Only an agent or a manager can issue this policy.')
    expect(screen.queryByRole('button', { name: 'Issue policy' })).not.toBeInTheDocument()
  })

  it('shows the server message when issuing is refused with a 403', async () => {
    const user = userEvent.setup()
    const onIssued = vi.fn()
    mockApi({
      'POST /api/policies/quote': () => json(quote),
      'POST /api/policies': () => problem(403, 'Only an Agent or a Manager can issue a policy.'),
    })
    renderAs(asha, <PolicyForm customers={customers} onIssued={onIssued} />)

    await fillForm(user)
    await user.click(getQuoteButton())
    await user.click(await screen.findByRole('button', { name: 'Issue policy' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Only an Agent or a Manager can issue a policy.')
    expect(onIssued).not.toHaveBeenCalled()
  })
})
