import type { Page } from '@playwright/test'

// The seeded users from docs/API.md, as they appear in the "Acting as" switcher.
export const ASHA = 'Asha (Agent)'
export const RAVI = 'Ravi (Adjuster)'
export const MEERA = 'Meera (Adjuster)'
export const KIRAN = 'Kiran (Manager)'

export async function actAs(page: Page, user: string) {
  await page.getByLabel('Acting as').selectOption({ label: user })
}

/** A date relative to today as "yyyy-MM-dd" (UTC, like the server's idea of today). */
export function daysFromToday(days: number): string {
  const date = new Date()
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}
