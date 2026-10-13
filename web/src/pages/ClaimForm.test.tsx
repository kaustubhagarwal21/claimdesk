import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { Policy } from '../api/types'
import { asha, json, mockApi, renderAs } from '../test/helpers'
import { ClaimForm } from './ClaimForm'

const policy: Policy = {
  id: 3,
  policyNumber: 'POL-0003',
  customerId: 11,
  customerName: 'Arjun Mehta',
  type: 'Motor',
  sumInsured: 400000,
  premium: 10000,
  startDate: '2026-04-01',
  endDate: '2027-03-31',
  status: 'Active',
  remainingCover: 150000,
}

describe('ClaimForm', () => {
  it('checks the policy term and the remaining cover before filing the claim', async () => {
    const user = userEvent.setup()
    const onFiled = vi.fn()
    const filed = { id: 9, claimNumber: 'CLM-0009' }
    const requests = mockApi({
      'GET /api/policies?status=Active': () => json([policy]),
      'POST /api/claims': () => json(filed, 201),
    })
    renderAs(asha, <ClaimForm onFiled={onFiled} />)

    await user.selectOptions(await screen.findByLabelText('Policy'), 'POL-0003 · Arjun Mehta · Motor')
    const incidentDate = screen.getByLabelText('Incident date')
    const amount = screen.getByLabelText('Claim amount (₹)')
    await user.type(incidentDate, '2026-03-31')
    await user.type(amount, '150001')
    await user.type(screen.getByLabelText('What happened'), 'Hit from behind at a traffic signal.')
    await user.click(screen.getByRole('button', { name: 'File claim' }))

    expect(incidentDate).toHaveAccessibleDescription(
      'The incident must fall inside the policy term, 01 Apr 2026 to 31 Mar 2027.',
    )
    expect(amount).toHaveAccessibleDescription('The amount cannot be more than the remaining cover of ₹1,50,000.00.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)

    await user.clear(incidentDate)
    await user.type(incidentDate, '2026-09-20')
    await user.clear(amount)
    await user.type(amount, '150000')
    await user.click(screen.getByRole('button', { name: 'File claim' }))

    await vi.waitFor(() => expect(onFiled).toHaveBeenCalledWith(filed))
    expect(requests.find((request) => request.method === 'POST')?.body).toEqual({
      policyId: 3,
      incidentDate: '2026-09-20',
      amount: 150000,
      description: 'Hit from behind at a traffic signal.',
    })
  })
})
