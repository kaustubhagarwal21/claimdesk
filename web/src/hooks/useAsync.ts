import { useCallback, useEffect, useState } from 'react'
import { type ApiError, toApiError } from '../api/client'

export interface AsyncState<T> {
  data: T | null
  error: ApiError | null
  loading: boolean
  reload: () => void
}

/**
 * Runs `load` when the component mounts, whenever `load` changes, and when
 * `reload` is called. Callers wrap `load` in useCallback so that it only
 * changes when its inputs (a filter, an id) change.
 *
 * The previous data stays visible during a reload, so the page does not flicker.
 */
export function useAsync<T>(load: () => Promise<T>): AsyncState<T> {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  const [loading, setLoading] = useState(true)
  const [reloadCount, setReloadCount] = useState(0)

  useEffect(() => {
    // If the inputs change before the response arrives, the old response is ignored.
    let cancelled = false
    setLoading(true)

    load()
      .then((result) => {
        if (cancelled) return
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(toApiError(err))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [load, reloadCount])

  const reload = useCallback(() => setReloadCount((count) => count + 1), [])

  return { data, error, loading, reload }
}
