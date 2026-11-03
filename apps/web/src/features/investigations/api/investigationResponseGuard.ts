import type { TicketInvestigation } from '../types/investigation'

const REQUIRED_LIST_FIELDS = [
  'features',
  'confirmedBugs',
  'candidateBugs',
  'relatedTickets',
  'knowledgeArticles',
] as const

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export function isTicketInvestigation(value: unknown): value is TicketInvestigation {
  if (!isRecord(value) || !isRecord(value.ticket) || !isRecord(value.counts)) {
    return false
  }

  const hasTicketId = typeof value.ticket.id === 'string'
  const hasCustomerShape = value.customer === null || isRecord(value.customer)
  const hasAllLists = REQUIRED_LIST_FIELDS.every((field) => Array.isArray(value[field]))

  return hasTicketId && hasCustomerShape && hasAllLists
}

export class UnexpectedInvestigationResponseError extends Error {
  constructor() {
    super('The investigation response did not match the expected contract.')
    this.name = 'UnexpectedInvestigationResponseError'
  }
}
