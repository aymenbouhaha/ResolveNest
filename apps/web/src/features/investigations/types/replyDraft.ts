import type { AiAnswerProvenance, AiStatement, CitationSource } from './investigationAnswer'

export type ReplyDraftStatus = 'drafted' | 'insufficientEvidence'

export interface ContextLimitation {
  code: string
  message: string
  subjectIds: string[]
}

export interface ReplyDraftPreviewRequest {
  reviewerGuidance?: string
}

export interface ReplyDraftPreview {
  ticketId: string
  previewId: string
  status: ReplyDraftStatus
  draftBody: string
  supportingClaims: AiStatement[]
  uncertainties: AiStatement[]
  evidenceLimitations: ContextLimitation[]
  validatedSources: CitationSource[]
  provenance: AiAnswerProvenance
  previewToken: string
  previewExpiresAt: string
  humanReviewRequired: boolean
  persisted: boolean
}

export interface ReplyDraftApprovalRequest {
  previewToken: string
  finalText: string
  approved: true
}

export interface ApprovedReplyDraft {
  draftId: string
  ticketId: string
  status: string
  approvedText: string
  generatedText: string
  editedByHuman: boolean
  generatedTextValidated: boolean
  approvedTextValidated: boolean
  citationIds: string[]
  limitationCodes: string[]
  model: string
  modelVersion: string | null
  evidenceContractVersion: string
  generatedAt: string
  approvedAt: string
  approvalBasis: string
  customerDelivery: string
}

export interface ReplyDraftApprovalResult {
  draft: ApprovedReplyDraft
  alreadyApproved: boolean
}
