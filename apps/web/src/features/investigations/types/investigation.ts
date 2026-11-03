export type EvidenceClassification = 'confirmed' | 'documented' | 'candidate' | 'context'

export type CandidateRelevanceTier =
  | 'errorCodeMatch'
  | 'versionCompatible'
  | 'featureOnly'
  | 'errorCodeMatchChronologyConflict'
  | 'versionConflict'

export type ErrorCodeMatchStatus = 'matched' | 'differentCodes' | 'unavailable'

export type VersionCompatibility =
  | 'withinDocumentedRange'
  | 'indeterminate'
  | 'outsideDocumentedRange'
  | 'noReleaseEvidence'
  | 'reportedVersionUnknown'

export type ReleaseBoundCheck =
  | 'consistent'
  | 'inconsistent'
  | 'indeterminate'
  | 'notDocumented'
  | 'notEvaluated'

export interface InvestigatedTicket {
  id: string
  type: string
  subject: string | null
  description: string | null
  status: string | null
  priority: string | null
  reportedVersion: string | null
  reportedVersionNormalized: string | null
  reportedVersionPrecision: string | null
  resolution: string | null
  createdAt: string | null
  resolvedAt: string | null
  unresolvedBugRefs: string[]
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
}

export interface ReportingCustomer {
  id: string
  type: string
  name: string | null
  plan: string | null
  deployment: string | null
  evidence: EvidenceClassification
  path: string
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
  relationshipSourceRecord: string | null
}

export interface TicketFeature {
  id: string
  type: string
  name: string | null
  evidence: EvidenceClassification
  path: string
  sourceSystem: string | null
  sourceRecord: string | null
  relationshipSourceRecord: string | null
}

export interface ReleaseEvidence {
  id: string
  version: string | null
  releasedAt: string | null
  evidence: EvidenceClassification
  evidenceText: string | null
  relationshipSourceRecord: string | null
}

export interface AffectedFeature {
  id: string
  name: string | null
  evidence: EvidenceClassification
  relationshipSourceRecord: string | null
}

export interface ConfirmedBug {
  id: string
  type: string
  title: string | null
  sourceStatus: string | null
  severity: string | null
  workaround: string | null
  evidence: EvidenceClassification
  path: string
  confirmedBy: string | null
  confirmedAt: string | null
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
  relationshipSourceRecord: string | null
  affectedFeatures: AffectedFeature[]
  introducedIn: ReleaseEvidence[]
  fixedIn: ReleaseEvidence[]
}

export interface SharedFeature {
  featureId: string
  featureName: string | null
  relationshipSourceRecord: string | null
}

export interface CandidateConfirmedTicket {
  id: string
  type: string
  subject: string | null
  status: string | null
  reportedVersion: string | null
  reportedVersionPrecision: string | null
  customerId: string | null
  sameCustomer: boolean
  evidence: EvidenceClassification
  path: string
  confirmedBy: string | null
  confirmedAt: string | null
  sourceSystem: string | null
  sourceRecord: string | null
  relationshipSourceRecord: string | null
}

export interface ErrorCodeSignal {
  status: ErrorCodeMatchStatus
  matchedCodes: string[]
  ticketCodes: string[]
  bugCodes: string[]
}

export interface VersionSignal {
  assessment: VersionCompatibility
  reportedVersion: string | null
  reportedVersionPrecision: string | null
  introducedInVersions: string[]
  fixedInVersions: string[]
  introducedCheck: ReleaseBoundCheck
  fixedCheck: ReleaseBoundCheck
}

export interface RelevanceNote {
  code: string
  message: string
}

export interface CandidateRelevance {
  tier: CandidateRelevanceTier
  sharedFeatureIds: string[]
  errorCodes: ErrorCodeSignal
  version: VersionSignal
  supporting: RelevanceNote[]
  limitations: RelevanceNote[]
}

export interface CandidateBug {
  id: string
  type: string
  title: string | null
  description: string | null
  sourceStatus: string | null
  severity: string | null
  workaround: string | null
  evidence: EvidenceClassification
  path: string
  reason: string
  sharedFeatures: SharedFeature[]
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
  introducedIn: ReleaseEvidence[]
  fixedIn: ReleaseEvidence[]
  confirmedTicketCount: number
  confirmedCustomerCount: number
  confirmedTickets: CandidateConfirmedTicket[]
  confirmedTicketLimit: number
  confirmedTicketsTruncated: boolean
  relevance: CandidateRelevance
}

export interface RelatedTicket {
  id: string
  type: string
  subject: string | null
  status: string | null
  createdAt: string | null
  reportedVersion: string | null
  reportedVersionPrecision: string | null
  customerId: string | null
  sameCustomer: boolean
  evidence: EvidenceClassification
  path: string
  reason: string
  sharedFeatureIds: string[]
  confirmedBugIds: string[]
  sourceSystem: string | null
  sourceRecord: string | null
}

export interface KnowledgeRoute {
  relationship: string
  targetId: string
  basis: string | null
  evidence: EvidenceClassification
  relationshipSourceRecord: string | null
}

export interface KnowledgeArticleEvidence {
  id: string
  type: string
  title: string | null
  summary: string | null
  updatedAt: string | null
  sourceSystem: string | null
  sourceId: string | null
  sourceRecord: string | null
  sourcePath: string | null
  reachedVia: KnowledgeRoute[]
  content: string | null
}

export interface InvestigationResultCounts {
  features: number
  confirmedBugs: number
  candidateBugs: number
  candidateBugLimit: number
  candidateBugLimitReached: boolean
  relatedTickets: number
  relatedTicketTotal: number
  relatedTicketLimit: number
  relatedTicketsTruncated: boolean
  knowledgeArticles: number
  knowledgeContentIncluded: boolean
}

export interface TicketInvestigation {
  ticket: InvestigatedTicket
  customer: ReportingCustomer | null
  features: TicketFeature[]
  confirmedBugs: ConfirmedBug[]
  candidateBugs: CandidateBug[]
  relatedTickets: RelatedTicket[]
  knowledgeArticles: KnowledgeArticleEvidence[]
  counts: InvestigationResultCounts
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
