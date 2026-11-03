import { useCallback, useEffect, useState } from 'react'
import { fetchTicketInvestigation } from '../api/investigationsApi'
import {
  isRequestCancelled,
  toInvestigationError,
  type InvestigationError,
} from '../api/investigationErrors'
import type { TicketInvestigation } from '../types/investigation'

export type InvestigationState =
  | { status: 'loading' }
  | { status: 'success'; investigation: TicketInvestigation }
  | { status: 'error'; error: InvestigationError }

type SettledInvestigation = Exclude<InvestigationState, { status: 'loading' }> & {
  requestKey: string
}

const LOADING_STATE: InvestigationState = { status: 'loading' }

export function useInvestigation(ticketId: string) {
  const [attempt, setAttempt] = useState(0)
  const [settled, setSettled] = useState<SettledInvestigation | null>(null)
  const requestKey = `${ticketId}#${attempt}`

  useEffect(() => {
    const controller = new AbortController()

    fetchTicketInvestigation(ticketId, { includeContent: true, signal: controller.signal })
      .then((investigation) => {
        if (!controller.signal.aborted) {
          setSettled({ requestKey, status: 'success', investigation })
        }
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || isRequestCancelled(error)) {
          return
        }
        setSettled({ requestKey, status: 'error', error: toInvestigationError(error) })
      })

    return () => controller.abort()
  }, [ticketId, requestKey])

  const reload = useCallback(() => setAttempt((current) => current + 1), [])

  const state: InvestigationState =
    settled !== null && settled.requestKey === requestKey ? settled : LOADING_STATE

  return { state, reload }
}
