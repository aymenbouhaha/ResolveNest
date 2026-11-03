import type { CandidateBug, RelevanceNote } from '../types/investigation'
import { pluralize } from './formatters'
import { formatReference, joinWithAnd } from './recordReferences'

function quotedVersion(version: string | null): string {
  return version ? `'${version}'` : 'no version'
}

function listItems(items: string[]): string {
  return joinWithAnd(items)
}

function ticketClosureVerb(ticketStatus: string | null): string {
  return ticketStatus === 'closed' ? 'closed' : 'resolved'
}

function sharedFeatureNames(bug: CandidateBug): string {
  return joinWithAnd(
    bug.sharedFeatures.map((feature) => feature.featureName ?? formatReference(feature.featureId)),
  )
}

type NoteTextBuilder = (bug: CandidateBug, ticketStatus: string | null) => string

const NOTE_TEXT_BUILDERS: Record<string, NoteTextBuilder> = {
  sharedFeature: (bug) => `Affects ${sharedFeatureNames(bug)}, the same feature as this ticket.`,
  errorCodeMatch: (bug) =>
    `The ticket and this issue report the same error: ${listItems(bug.relevance.errorCodes.matchedCodes)}.`,
  differentErrorCodes: (bug) =>
    `Different errors are reported: ${listItems(bug.relevance.errorCodes.ticketCodes)} on the ticket, ${listItems(bug.relevance.errorCodes.bugCodes)} on this issue.`,
  noErrorCodeInTicket: () => 'The ticket does not mention a specific error code to compare.',
  noErrorCodeInBug: () => "This issue's record does not mention a specific error code to compare.",
  versionWithinDocumentedRange: (bug) =>
    `The reported version ${quotedVersion(bug.relevance.version.reportedVersion)} appears to fall within the releases this issue affects.`,
  versionMayBeWithinDocumentedRange: (bug) =>
    `The reported version ${quotedVersion(bug.relevance.version.reportedVersion)} may be affected, depending on the exact patch installed.`,
  versionOutsideDocumentedRange: (bug) =>
    bug.relevance.version.introducedCheck === 'inconsistent'
      ? `The reported version ${quotedVersion(bug.relevance.version.reportedVersion)} is older than the release that introduced this issue (${listItems(bug.relevance.version.introducedInVersions)}). The version report may be inaccurate.`
      : `The reported version ${quotedVersion(bug.relevance.version.reportedVersion)} already includes the fix (${listItems(bug.relevance.version.fixedInVersions)}). This could mean the version report is inaccurate or the problem has returned.`,
  noReleaseEvidence: () =>
    'No affected or fixed release is recorded for this issue, so the version cannot be checked.',
  reportedVersionUnknown: (bug) =>
    `The ticket's version (${quotedVersion(bug.relevance.version.reportedVersion)}) cannot be compared with this issue's releases.`,
  patchVersionUnknown: (bug) =>
    `The customer reported ${quotedVersion(bug.relevance.version.reportedVersion)}, not an exact patch version.`,
  introductionReleaseNotDocumented: () =>
    'Only the fix release is recorded. When the issue was introduced is unknown.',
  fixDoesNotProveResolution: (bug) =>
    `A fix shipped in ${listItems(bug.relevance.version.fixedInVersions)}. That does not confirm the customer has upgraded or that their problem is solved.`,
  historicalConfirmations: (bug) =>
    `Confirmed on ${pluralize(bug.confirmedTicketCount, 'other support case')} from ${pluralize(bug.confirmedCustomerCount, 'customer')}. This supports relevance but does not confirm it for this ticket.`,
  noHistoricalConfirmations: () => 'No other support case has been confirmed against this issue.',
  ticketHasConfirmedBug: () =>
    'This ticket already has a confirmed issue. This one is shown for comparison only.',
  ticketClosedWithoutConfirmedBug: (_bug, ticketStatus) =>
    `This ticket was ${ticketClosureVerb(ticketStatus)} without linking it to a confirmed product defect. Its recorded resolution may point to a different cause.`,
  errorCodeMatchContradictedByIntroduction: (bug) =>
    `The error ${listItems(bug.relevance.errorCodes.matchedCodes)} matches, but the reported version ${quotedVersion(bug.relevance.version.reportedVersion)} is older than the release that introduced this issue (${listItems(bug.relevance.version.introducedInVersions)}). Unless the version report is wrong, the release history argues against this connection.`,
}

export interface PresentedRelevanceNote {
  code: string
  text: string
}

export function presentRelevanceNote(
  bug: CandidateBug,
  note: RelevanceNote,
  ticketStatus: string | null,
): PresentedRelevanceNote {
  const buildText = NOTE_TEXT_BUILDERS[note.code]
  return { code: note.code, text: buildText ? buildText(bug, ticketStatus) : note.message }
}
