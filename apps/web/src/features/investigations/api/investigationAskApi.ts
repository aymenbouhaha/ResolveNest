import { apiClient } from '@/lib/axios'
import type { AskInvestigationRequest, InvestigationAnswer } from '../types/investigationAnswer'
import {
  isInvestigationAnswer,
  UnexpectedInvestigationAnswerError,
} from './investigationAnswerGuard'

export const AI_ANSWER_REQUEST_TIMEOUT_MS = 75_000

export interface AskInvestigationOptions {
  signal?: AbortSignal
}

export async function askInvestigationQuestion(
  ticketId: string,
  question: string,
  { signal }: AskInvestigationOptions = {},
): Promise<InvestigationAnswer> {
  const body: AskInvestigationRequest = { question }
  const response = await apiClient.post<unknown>(
    `/investigations/tickets/${encodeURIComponent(ticketId)}/ask`,
    body,
    {
      signal,
      timeout: AI_ANSWER_REQUEST_TIMEOUT_MS,
      headers: { 'Content-Type': 'application/json' },
    },
  )

  if (!isInvestigationAnswer(response.data) || response.data.ticketId !== ticketId) {
    throw new UnexpectedInvestigationAnswerError()
  }

  return response.data
}
