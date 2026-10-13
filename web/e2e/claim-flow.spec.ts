import { expect, test } from '@playwright/test'
import { actAs, ASHA, daysFromToday, KIRAN, MEERA, RAVI } from './helpers'

// One claim from issue to payment, through the real UI, API and database.
// Every run adds one policy and one claim to the development database.
test('a claim goes from filing to payment, with maker-checker and an audit trail', async ({ page }) => {
  // The step shown as current in the progress track is the claim's status.
  const currentStep = page.getByRole('list', { name: 'Claim progress' }).locator('[aria-current="step"]')

  // Asha (Agent) quotes and issues a Motor policy.
  await page.goto('/policies')
  await actAs(page, ASHA)
  await page.getByLabel('Customer', { exact: true }).selectOption({ label: 'Neha Sharma' })
  await page.getByLabel('Policy type').selectOption('Motor')
  await page.getByLabel('Sum insured (₹)').fill('500000')
  await page.getByLabel('Start date').fill(daysFromToday(-30))
  await page.getByRole('button', { name: 'Get quote' }).click()
  await expect(page.getByRole('group', { name: 'Quote' })).toContainText('₹12,500.00') // 2.5% of 5,00,000
  await page.getByRole('button', { name: 'Issue policy' }).click()

  const issued = page.getByText(/Policy POL-\d{6} issued for Neha Sharma/)
  await expect(issued).toBeVisible()
  const policyNumber = (await issued.innerText()).match(/POL-\d{6}/)![0]

  // Asha files a claim on that policy.
  await page.getByRole('link', { name: 'Claims' }).click()
  await page.getByLabel('Policy', { exact: true }).selectOption({ label: `${policyNumber} · Neha Sharma · Motor` })
  await page.getByLabel('Incident date').fill(daysFromToday(-3))
  await page.getByLabel('Claim amount (₹)').fill('42000')
  await page.getByLabel('What happened').fill('Rear bumper damaged in a parking collision')
  await page.getByRole('button', { name: 'File claim' }).click()

  await page.getByRole('link', { name: /CLM-\d{6}/ }).first().click()
  await expect(page.getByRole('heading', { name: /CLM-\d{6}/ })).toBeVisible()
  await expect(currentStep).toHaveText('Submitted')

  // Ravi (Adjuster) starts the review.
  await actAs(page, RAVI)
  await page.getByRole('button', { name: 'Start review' }).click()
  await expect(currentStep).toHaveText('Under review')

  // Maker-checker: the UI does not offer Ravi the Approve button, and the API refuses him too.
  await expect(page.getByText('You reviewed this claim, so someone else has to approve it.')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Approve' })).toHaveCount(0)

  const claimId = page.url().split('/').pop()
  const claim = await (await page.request.get(`/api/claims/${claimId}`, { headers: { 'X-User-Id': '2' } })).json()
  const refused = await page.request.post(`/api/claims/${claimId}/approve`, {
    headers: { 'X-User-Id': '2' },
    data: { rowVersion: claim.rowVersion },
  })
  expect(refused.status()).toBe(409)

  // Meera (a different Adjuster) approves it.
  await actAs(page, MEERA)
  await page.getByRole('button', { name: 'Approve' }).click()
  await expect(currentStep).toHaveText('Approved')

  // Kiran (Manager) records the payment.
  await actAs(page, KIRAN)
  await page.getByRole('button', { name: 'Mark as paid' }).click()
  await expect(currentStep).toHaveText('Paid')

  // The trigger wrote one audit row per status change, in order, with the user who made it.
  const audit = page.getByRole('list', { name: 'Audit trail' }).getByRole('listitem')
  await expect(audit).toHaveText([
    /Filed as Submitted.*Asha/,
    /Submitted → Under review.*Ravi/,
    /Under review → Approved.*Meera/,
    /Approved → Paid.*Kiran/,
  ])
})
