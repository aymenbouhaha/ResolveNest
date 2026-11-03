import type { EvidenceClassification } from './investigation'

export type AiAnswerStatus = 'answered' | 'insufficientEvidence'

export type AiFindingKind =
  | 'confirmedForTicket'
  | 'historicalConfirmation'
  | 'candidate'
  | 'documented'
  | 'context'
  | 'contradiction'

export type CitationKind = 'record' | 'relationship'

export interface AiFinding {
  kind: AiFindingKind
  statement: string
  citationIds: string[]
}

export interface AiStatement {
  statement: string
  citationIds: string[]
}

export interface CitedRelationship {
  type: string
  fromId: string
  toId: string
  evidence: EvidenceClassification
  path: string
}

export interface CitationSource {
  citationId: string
  kind: CitationKind
  entityId: string
  entityType: string
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
  sourcePath: string | null
  relationship: CitedRelationship | null
}

export interface AiAnswerProvenance {
  provider: string
  model: string
  modelVersion: string | null
  evidenceContractVersion: string
  evidenceComplete: boolean
  evidenceCharacters: number
  promptTokens: number | null
  outputTokens: number | null
  humanReviewRequired: boolean
}

export interface InvestigationAnswer {
  ticketId: string
  status: AiAnswerStatus
  answer: string
  findings: AiFinding[]
  uncertainties: AiStatement[]
  recommendedNextSteps: AiStatement[]
  validatedSources: CitationSource[]
  provenance: AiAnswerProvenance
}

export interface AskInvestigationRequest {
  question: string
}
