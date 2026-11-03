import type { InvestigationAnswer } from '../types/investigationAnswer'

const KNOWN_ANSWER_STATUSES = new Set(['answered', 'insufficientEvidence'])
const STATEMENT_LIST_FIELDS = ['findings', 'uncertainties', 'recommendedNextSteps'] as const

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isStatementList(value: unknown): boolean {
  return (
    Array.isArray(value) &&
    value.every(
      (item) =>
        isRecord(item) && typeof item.statement === 'string' && Array.isArray(item.citationIds),
    )
  )
}

function isSourceList(value: unknown): boolean {
  return (
    Array.isArray(value) &&
    value.every((item) => isRecord(item) && typeof item.citationId === 'string')
  )
}

export function isInvestigationAnswer(value: unknown): value is InvestigationAnswer {
  if (!isRecord(value) || !isRecord(value.provenance)) {
    return false
  }

  return (
    typeof value.ticketId === 'string' &&
    typeof value.status === 'string' &&
    KNOWN_ANSWER_STATUSES.has(value.status) &&
    typeof value.answer === 'string' &&
    STATEMENT_LIST_FIELDS.every((field) => isStatementList(value[field])) &&
    isSourceList(value.validatedSources)
  )
}

export class UnexpectedInvestigationAnswerError extends Error {
  constructor() {
    super('The investigation answer did not match the expected contract.')
    this.name = 'UnexpectedInvestigationAnswerError'
  }
}
