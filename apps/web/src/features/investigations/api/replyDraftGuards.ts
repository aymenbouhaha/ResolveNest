import type { ReplyDraftApprovalResult, ReplyDraftPreview } from '../types/replyDraft'

const KNOWN_DRAFT_STATUSES = new Set(['drafted', 'insufficientEvidence'])

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

export function isReplyDraftPreview(value: unknown): value is ReplyDraftPreview {
  return (
    isRecord(value) &&
    typeof value.ticketId === 'string' &&
    typeof value.previewId === 'string' &&
    typeof value.status === 'string' &&
    KNOWN_DRAFT_STATUSES.has(value.status) &&
    typeof value.draftBody === 'string' &&
    typeof value.previewToken === 'string' &&
    value.previewToken.length > 0 &&
    typeof value.previewExpiresAt === 'string' &&
    value.persisted === false &&
    isStatementList(value.supportingClaims) &&
    isStatementList(value.uncertainties) &&
    Array.isArray(value.evidenceLimitations) &&
    Array.isArray(value.validatedSources) &&
    isRecord(value.provenance)
  )
}

export function isReplyDraftApprovalResult(value: unknown): value is ReplyDraftApprovalResult {
  return (
    isRecord(value) &&
    typeof value.alreadyApproved === 'boolean' &&
    isRecord(value.draft) &&
    typeof value.draft.draftId === 'string' &&
    typeof value.draft.ticketId === 'string' &&
    typeof value.draft.approvedText === 'string' &&
    typeof value.draft.approvedAt === 'string'
  )
}

export class UnexpectedReplyDraftResponseError extends Error {
  constructor() {
    super('The reply draft response did not match the expected contract.')
    this.name = 'UnexpectedReplyDraftResponseError'
  }
}
