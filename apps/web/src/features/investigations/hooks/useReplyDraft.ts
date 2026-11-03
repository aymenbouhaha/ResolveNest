import { useCallback, useEffect, useRef, useState } from 'react'
import { approveReplyDraft, generateReplyDraftPreview } from '../api/replyDraftApi'
import {
  toApprovalError,
  toPreviewError,
  type ApprovalError,
  type ApprovalErrorKind,
  type ReplyDraftError,
} from '../api/replyDraftErrors'
import { isRequestCancelled } from '../api/investigationErrors'
import type { ReplyDraftApprovalResult, ReplyDraftPreview } from '../types/replyDraft'

export type ReplyDraftState =
  | { phase: 'idle' }
  | { phase: 'generating' }
  | { phase: 'generationFailed'; error: ReplyDraftError }
  | { phase: 'reviewing'; preview: ReplyDraftPreview; draftText: string }
  | { phase: 'approving'; preview: ReplyDraftPreview; draftText: string; submittedText: string }
  | {
      phase: 'approvalFailed'
      preview: ReplyDraftPreview
      draftText: string
      submittedText: string
      error: ApprovalError
    }
  | { phase: 'approved'; preview: ReplyDraftPreview; result: ReplyDraftApprovalResult }

const IDLE_STATE: ReplyDraftState = { phase: 'idle' }

export const EDITABLE_AFTER_FAILURE = new Set<ApprovalErrorKind>(['invalidText', 'notSavedDatabaseUnavailable'])

export function useReplyDraft(ticketId: string) {
  const [state, setState] = useState<ReplyDraftState>(IDLE_STATE)
  const previewRequestRef = useRef<AbortController | null>(null)
  const isApprovingRef = useRef(false)
  const isMountedRef = useRef(true)

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
      previewRequestRef.current?.abort()
      previewRequestRef.current = null
    }
  }, [ticketId])

  const generatePreview = useCallback(
    async (reviewerGuidance: string | null) => {
      if (previewRequestRef.current !== null || isApprovingRef.current) {
        return
      }

      const controller = new AbortController()
      previewRequestRef.current = controller
      setState({ phase: 'generating' })

      try {
        const preview = await generateReplyDraftPreview(ticketId, reviewerGuidance, controller.signal)
        if (!controller.signal.aborted) {
          setState({ phase: 'reviewing', preview, draftText: preview.draftBody })
        }
      } catch (error: unknown) {
        if (!controller.signal.aborted && !isRequestCancelled(error)) {
          setState({ phase: 'generationFailed', error: toPreviewError(error) })
        }
      } finally {
        if (previewRequestRef.current === controller) {
          previewRequestRef.current = null
        }
      }
    },
    [ticketId],
  )

  const cancelPreview = useCallback(() => {
    previewRequestRef.current?.abort()
    previewRequestRef.current = null
    setState(IDLE_STATE)
  }, [])

  const updateDraftText = useCallback((draftText: string) => {
    setState((current) => {
      if (current.phase === 'reviewing') {
        return { ...current, draftText }
      }
      if (current.phase === 'approvalFailed' && EDITABLE_AFTER_FAILURE.has(current.error.kind)) {
        return { phase: 'reviewing', preview: current.preview, draftText }
      }
      return current
    })
  }, [])

  const submitApproval = useCallback(
    async (preview: ReplyDraftPreview, draftText: string, finalText: string) => {
      if (isApprovingRef.current || previewRequestRef.current !== null) {
        return
      }

      isApprovingRef.current = true
      setState({ phase: 'approving', preview, draftText, submittedText: finalText })

      try {
        const result = await approveReplyDraft(ticketId, preview.previewToken, finalText)
        if (isMountedRef.current) {
          setState({ phase: 'approved', preview, result })
        }
      } catch (error: unknown) {
        if (isMountedRef.current) {
          setState({
            phase: 'approvalFailed',
            preview,
            draftText,
            submittedText: finalText,
            error: toApprovalError(error),
          })
        }
      } finally {
        isApprovingRef.current = false
      }
    },
    [ticketId],
  )

  const approve = useCallback(
    (finalText: string) => {
      if (state.phase !== 'reviewing') {
        return
      }
      void submitApproval(state.preview, state.draftText, finalText)
    },
    [state, submitApproval],
  )

  const resendApproval = useCallback(() => {
    if (state.phase !== 'approvalFailed' || !state.error.retryable) {
      return
    }
    void submitApproval(state.preview, state.draftText, state.submittedText)
  }, [state, submitApproval])

  const startOver = useCallback(() => {
    if (isApprovingRef.current) {
      return
    }
    previewRequestRef.current?.abort()
    previewRequestRef.current = null
    setState(IDLE_STATE)
  }, [])

  return {
    state,
    generatePreview,
    cancelPreview,
    updateDraftText,
    approve,
    resendApproval,
    startOver,
  }
}
