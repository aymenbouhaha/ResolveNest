import { apiClient } from '@/lib/axios'
import type { TicketInvestigation } from '../types/investigation'
import {
  isTicketInvestigation,
  UnexpectedInvestigationResponseError,
} from './investigationResponseGuard'

export interface TicketInvestigationRequestOptions {
  includeContent?: boolean
  signal?: AbortSignal
}

export async function fetchTicketInvestigation(
  ticketId: string,
  { includeContent = false, signal }: TicketInvestigationRequestOptions = {},
): Promise<TicketInvestigation> {
  const response = await apiClient.get<unknown>(
    `/investigations/tickets/${encodeURIComponent(ticketId)}`,
    {
      params: { includeContent },
      signal,
    },
  )

  if (!isTicketInvestigation(response.data)) {
    throw new UnexpectedInvestigationResponseError()
  }

  return response.data
}
