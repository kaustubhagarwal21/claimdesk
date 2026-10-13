import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Every test starts with an empty page, the real fetch put back, and no stored user.
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  localStorage.clear()
})
