import type {
  CandidateRelevanceTier,
  ErrorCodeMatchStatus,
  EvidenceClassification,
  ReleaseBoundCheck,
  VersionCompatibility,
} from '../types/investigation'

export const EVIDENCE_CLASSIFICATION_LABELS: Record<
  EvidenceClassification,
  { label: string; description: string }
> = {
  confirmed: {
    label: 'Confirmed',
    description: 'A support engineer explicitly confirmed this link.',
  },
  documented: {
    label: 'Documented',
    description:
      'Recorded as fact in a source system, such as the customer record, the issue tracker or the release notes.',
  },
  candidate: {
    label: 'Possible',
    description:
      'A possible connection because it involves the same product feature. It has not been confirmed for this ticket.',
  },
  context: {
    label: 'Background',
    description: 'Shown for comparison. It does not indicate the cause of this ticket.',
  },
}

export const RELEVANCE_TIER_LABELS: Record<
  CandidateRelevanceTier,
  { label: string; summary: string }
> = {
  errorCodeMatch: {
    label: 'Matching error code',
    summary:
      'This issue may be related because it reports the same error code, and its release history does not rule it out.',
  },
  versionCompatible: {
    label: 'Version may match',
    summary:
      'No shared error code, but the reported version may fall within the releases this issue affects.',
  },
  featureOnly: {
    label: 'Same feature only',
    summary:
      'This issue affects the same product feature, but there is not enough evidence to confirm a connection.',
  },
  errorCodeMatchChronologyConflict: {
    label: "Error matches, dates don't align",
    summary:
      'The error codes match, but the reported version is older than the release that introduced this issue.',
  },
  versionConflict: {
    label: 'Version mismatch',
    summary:
      'No shared error code, and the reported version falls outside the releases this issue affects.',
  },
}

export const ERROR_CODE_STATUS_LABELS: Record<ErrorCodeMatchStatus, string> = {
  matched: 'Same error code',
  differentCodes: 'Different error codes',
  unavailable: 'No error code to compare',
}

export const VERSION_ASSESSMENT_LABELS: Record<VersionCompatibility, string> = {
  withinDocumentedRange: 'Appears within affected releases',
  indeterminate: 'May be within affected releases',
  outsideDocumentedRange: 'Outside affected releases',
  noReleaseEvidence: 'No release history recorded',
  reportedVersionUnknown: 'Reported version not comparable',
}

export const RELEASE_BOUND_CHECK_LABELS: Record<ReleaseBoundCheck, string> = {
  consistent: 'consistent with the report',
  inconsistent: 'conflicts with the report',
  indeterminate: 'depends on the exact patch',
  notDocumented: 'not recorded',
  notEvaluated: 'cannot be compared',
}
