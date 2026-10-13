import { type APIRequestContext, expect, test } from '@playwright/test'
import { actAs, daysFromToday, RAVI } from './helpers'

// Saves the three screenshots used in the README:  npm run screenshots
// If the database has fewer than five claims, sample data is added through the API first.

const OUT = '../docs/screenshots'

// The seeded user ids from docs/API.md.
const ASHA_ID = 1
const RAVI_ID = 2
const MEERA_ID = 3
const KIRAN_ID = 4

async function post(request: APIRequestContext, userId: number, path: string, data?: object) {
  const response = await request.post(`/api${path}`, { headers: { 'X-User-Id': String(userId) }, data })
  expect(response.ok(), `POST ${path} as user ${userId}`).toBeTruthy()
  return response.json()
}

async function getClaims(request: APIRequestContext) {
  const response = await request.get('/api/claims', { headers: { 'X-User-Id': String(ASHA_ID) } })
  return (await response.json()) as { id: number; status: string }[]
}

// Three policies and five claims, one in each status.
async function addSampleData(request: APIRequestContext) {
  const startDate = daysFromToday(-60)
  const incidentDate = daysFromToday(-7)
  const customers = await (await request.get('/api/customers', { headers: { 'X-User-Id': String(ASHA_ID) } })).json()

  const issue = (customerId: number, type: string, sumInsured: number) =>
    post(request, ASHA_ID, '/policies', { customerId, type, sumInsured, startDate })
  const file = (policyId: number, amount: number, description: string) =>
    post(request, ASHA_ID, '/claims', { policyId, incidentDate, amount, description })

  const motor = await issue(customers[0].id, 'Motor', 500_000)
  const health = await issue(customers[1].id, 'Health', 800_000)
  const home = await issue(customers[2].id, 'Home', 2_500_000)

  // Paid: filed by Asha, reviewed by Ravi, approved by Meera, paid by Kiran.
  let claim = await file(motor.id, 42_000, 'Rear bumper damaged in a parking collision')
  claim = await post(request, RAVI_ID, `/claims/${claim.id}/review`)
  claim = await post(request, MEERA_ID, `/claims/${claim.id}/approve`, { rowVersion: claim.rowVersion })
  await post(request, KIRAN_ID, `/claims/${claim.id}/pay`, { rowVersion: claim.rowVersion })

  // Approved: above 1,00,000, so only the manager could approve it.
  claim = await file(health.id, 180_000, 'Knee surgery and five days in hospital')
  claim = await post(request, RAVI_ID, `/claims/${claim.id}/review`)
  await post(request, KIRAN_ID, `/claims/${claim.id}/approve`, { rowVersion: claim.rowVersion })

  // Rejected.
  claim = await file(health.id, 12_000, 'Dental treatment at a private clinic')
  claim = await post(request, RAVI_ID, `/claims/${claim.id}/review`)
  await post(request, MEERA_ID, `/claims/${claim.id}/reject`, {
    reason: 'Dental treatment is not covered by this policy',
    rowVersion: claim.rowVersion,
  })

  // Under review.
  claim = await file(home.id, 65_000, 'Kitchen damaged by a burst water pipe')
  await post(request, MEERA_ID, `/claims/${claim.id}/review`)

  // Submitted.
  await file(motor.id, 18_500, 'Windscreen cracked by a stone on the highway')
}

test('README screenshots', { tag: '@screenshots' }, async ({ page, request }) => {
  let claims = await getClaims(request)
  if (claims.length < 5) {
    await addSampleData(request)
    claims = await getClaims(request)
  }
  const paidClaim = claims.find((claim) => claim.status === 'Paid')!

  await page.goto('/')
  await actAs(page, RAVI)
  await expect(page.getByRole('table')).toBeVisible()
  await page.evaluate(() => document.fonts.ready) // wait for the web fonts
  await page.screenshot({ path: `${OUT}/dashboard.png` })

  await page.goto('/claims')
  await expect(page.getByRole('table')).toBeVisible()
  await page.screenshot({ path: `${OUT}/claims.png` })

  await page.goto(`/claims/${paidClaim.id}`)
  await expect(page.getByRole('list', { name: 'Audit trail' })).toBeVisible()
  await page.screenshot({ path: `${OUT}/claim-detail.png` })
})
