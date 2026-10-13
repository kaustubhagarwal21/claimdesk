// The id of the user we are "acting as". It is kept in localStorage so the
// choice survives a page refresh, and the API client reads it for every request.

const STORAGE_KEY = 'claimdesk.userId'

export function getActingUserId(): number | null {
  const id = Number(localStorage.getItem(STORAGE_KEY))
  return Number.isInteger(id) && id > 0 ? id : null
}

export function setActingUserId(id: number): void {
  localStorage.setItem(STORAGE_KEY, String(id))
}
