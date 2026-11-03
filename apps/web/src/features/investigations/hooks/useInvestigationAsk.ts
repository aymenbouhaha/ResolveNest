import { useCallback, useEffect, useRef, useState } from 'react'
import { askInvestigationQuestion } from '../api/investigationAskApi'
import { toAskError, type AskError } from '../api/investigationAskErrors'
import { isRequestCancelled } from '../api/investigationErrors'
import type { InvestigationAnswer } from '../types/investigationAnswer'

export type InvestigationAskState =
  | { status: 'idle' }
  | { status: 'asking'; question: string }
  | { status: 'answered'; question: string; answer: InvestigationAnswer }
  | { status: 'failed'; question: string; error: AskError }

const IDLE_STATE: InvestigationAskState = { status: 'idle' }

export function useInvestigationAsk(ticketId: string) {
  const [state, setState] = useState<InvestigationAskState>(IDLE_STATE)
  const activeRequestRef = useRef<AbortController | null>(null)

  useEffect(
    () => () => {
      activeRequestRef.current?.abort()
      activeRequestRef.current = null
    },
    [ticketId],
  )

  const ask = useCallback(
    async (question: string) => {
      if (activeRequestRef.current !== null) {
        return
      }

      const controller = new AbortController()
      activeRequestRef.current = controller
      setState({ status: 'asking', question })

      try {
        const answer = await askInvestigationQuestion(ticketId, question, {
          signal: controller.signal,
        })
        if (!controller.signal.aborted) {
          setState({ status: 'answered', question, answer })
        }
      } catch (error: unknown) {
        if (!controller.signal.aborted && !isRequestCancelled(error)) {
          setState({ status: 'failed', question, error: toAskError(error) })
        }
      } finally {
        if (activeRequestRef.current === controller) {
          activeRequestRef.current = null
        }
      }
    },
    [ticketId],
  )

  const cancel = useCallback(() => {
    activeRequestRef.current?.abort()
    activeRequestRef.current = null
    setState(IDLE_STATE)
  }, [])

  const clear = useCallback(() => {
    if (activeRequestRef.current === null) {
      setState(IDLE_STATE)
    }
  }, [])

  return { state, ask, cancel, clear }
}
