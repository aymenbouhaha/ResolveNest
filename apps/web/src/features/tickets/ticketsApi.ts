import { apiClient } from '@/lib/axios'

export interface TicketSummary {
  id: string
  sourceId: string | null
  subject: string | null
  status: string | null
  priority: string | null
  reportedVersion: string | null
  createdAt: string | null
  updatedAt: string | null
  customer: { id: string; name: string | null } | null
}

export interface TicketPage {
  items: TicketSummary[]
  page: number
  pageSize: number
  totalCount: number
}

export async function fetchTickets(page: number, q: string, signal: AbortSignal): Promise<TicketPage> {
  const response = await apiClient.get<unknown>('/tickets', {
    params: { page, pageSize: 25, q: q || undefined },
    signal,
  })
  if (!isTicketPage(response.data)) throw new Error('Unexpected ticket list response')
  return response.data
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isTicketSummary(value: unknown): value is TicketSummary {
  if (!isRecord(value) || typeof value.id !== 'string') return false
  const optionalFields = ['sourceId', 'subject', 'status', 'priority', 'reportedVersion', 'createdAt', 'updatedAt']
  if (!optionalFields.every((field) => value[field] === null || typeof value[field] === 'string')) return false
  return value.customer === null || (isRecord(value.customer) && typeof value.customer.id === 'string' && (value.customer.name === null || typeof value.customer.name === 'string'))
}

function isTicketPage(value: unknown): value is TicketPage {
  return isRecord(value) && Array.isArray(value.items) && value.items.every(isTicketSummary)
    && Number.isSafeInteger(value.page) && Number(value.page) >= 1
    && Number.isSafeInteger(value.pageSize) && Number(value.pageSize) >= 1 && Number(value.pageSize) <= 100
    && Number.isSafeInteger(value.totalCount) && Number(value.totalCount) >= 0
}

export function shortTicketId(ticketId: string): string {
  return ticketId.replace(/^ticket:/, '')
}
