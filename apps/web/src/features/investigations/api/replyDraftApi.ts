import { apiClient } from '@/lib/axios'
import type {
  ReplyDraftApprovalRequest,
  ReplyDraftApprovalResult,
  ReplyDraftPreview,
  ReplyDraftPreviewRequest,
} from '../types/replyDraft'
import { AI_ANSWER_REQUEST_TIMEOUT_MS } from './investigationAskApi'
import {
  isReplyDraftApprovalResult,
  isReplyDraftPreview,
  UnexpectedReplyDraftResponseError,
} from './replyDraftGuards'

export const REPLY_APPROVAL_TIMEOUT_MS = 30_000

function replyDraftsPath(ticketId: string, action: 'preview' | 'approve'): string {
  return `/investigations/tickets/${encodeURIComponent(ticketId)}/reply-drafts/${action}`
}

export async function generateReplyDraftPreview(
  ticketId: string,
  reviewerGuidance: string | null,
  signal?: AbortSignal,
): Promise<ReplyDraftPreview> {
  const body: ReplyDraftPreviewRequest = reviewerGuidance ? { reviewerGuidance } : {}
  const response = await apiClient.post<unknown>(replyDraftsPath(ticketId, 'preview'), body, {
    signal,
    timeout: AI_ANSWER_REQUEST_TIMEOUT_MS,
    headers: { 'Content-Type': 'application/json' },
  })

  if (!isReplyDraftPreview(response.data) || response.data.ticketId !== ticketId) {
    throw new UnexpectedReplyDraftResponseError()
  }

  return response.data
}

export async function approveReplyDraft(
  ticketId: string,
  previewToken: string,
  finalText: string,
): Promise<ReplyDraftApprovalResult> {
  const body: ReplyDraftApprovalRequest = { previewToken, finalText, approved: true }
  const response = await apiClient.post<unknown>(replyDraftsPath(ticketId, 'approve'), body, {
    timeout: REPLY_APPROVAL_TIMEOUT_MS,
    headers: { 'Content-Type': 'application/json' },
  })

  if (!isReplyDraftApprovalResult(response.data) || response.data.draft.ticketId !== ticketId) {
    throw new UnexpectedReplyDraftResponseError()
  }

  return response.data
}
