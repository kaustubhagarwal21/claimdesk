import { describe, expect, it } from 'vitest'
import { formatDate, formatMoney } from './format'

describe('formatMoney', () => {
  it('formats amounts as Indian rupees with lakh and crore grouping', () => {
    expect(formatMoney(100000)).toBe('₹1,00,000.00')
    expect(formatMoney(12345678.5)).toBe('₹1,23,45,678.50')
    expect(formatMoney(0)).toBe('₹0.00')
  })
})

describe('formatDate', () => {
  it('shows an ISO date as day, month name and year', () => {
    expect(formatDate('2026-10-13')).toBe('13 Oct 2026')
    expect(formatDate('2027-01-05')).toBe('05 Jan 2027')
  })
})
