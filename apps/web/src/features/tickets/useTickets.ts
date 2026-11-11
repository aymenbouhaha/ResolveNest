import { useCallback, useEffect, useState } from 'react'
import { fetchTickets, type TicketPage } from './ticketsApi'

type SettledTickets = { key: string } & (
  | { status: 'success'; data: TicketPage }
  | { status: 'error' }
)

export function useTickets() {
  const [query, setQuery] = useState({ page: 1, q: '' })
  const [searchText, setSearchText] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [settled, setSettled] = useState<SettledTickets | null>(null)
  const key = JSON.stringify([query.page, query.q, attempt])

  useEffect(() => {
    const controller = new AbortController()
    fetchTickets(query.page, query.q, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setSettled({ key, status: 'success', data })
      })
      .catch(() => {
        if (!controller.signal.aborted) setSettled({ key, status: 'error' })
      })
    return () => controller.abort()
  }, [query.page, query.q, key])

  const reload = useCallback(() => setAttempt((value) => value + 1), [])
  const search = (q: string) => setQuery({ page: 1, q: q.trim() })
  const setPage = (page: number) => setQuery((value) => ({ ...value, page }))
  const state = settled?.key === key ? settled : { status: 'loading' as const }

  return { state, query, searchText, setSearchText, search, setPage, reload }
}

export type TicketList = ReturnType<typeof useTickets>
