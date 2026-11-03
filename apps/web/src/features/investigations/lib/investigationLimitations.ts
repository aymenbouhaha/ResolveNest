import type { TicketInvestigation } from '../types/investigation'
import { pluralize } from './formatters'
import { formatReference, joinWithAnd } from './recordReferences'

const CLOSED_TICKET_STATUSES = new Set(['resolved', 'closed'])

export type InvestigationLimitationKind = 'uncertainty' | 'coverage'

export interface InvestigationLimitation {
  key: string
  kind: InvestigationLimitationKind
  message: string
}

function describeMissingConfirmation(candidateCount: number): string {
  if (candidateCount === 0) {
    return 'No product issue has been confirmed for this ticket, and no potential related issue was found.'
  }
  const verb = candidateCount === 1 ? 'is' : 'are'
  return `No product issue has been confirmed for this ticket. ${pluralize(candidateCount, 'potential related issue')} ${verb} listed because ${candidateCount === 1 ? 'it affects' : 'they affect'} the same feature; none is confirmed.`
}

function collectUncertainties({
  ticket,
  confirmedBugs,
  candidateBugs,
}: TicketInvestigation): InvestigationLimitation[] {
  const uncertainties: InvestigationLimitation[] = []

  if (confirmedBugs.length === 0) {
    uncertainties.push({
      key: 'noConfirmedIssue',
      kind: 'uncertainty',
      message: describeMissingConfirmation(candidateBugs.length),
    })
  }

  if (confirmedBugs.length === 0 && CLOSED_TICKET_STATUSES.has(ticket.status ?? '')) {
    uncertainties.push({
      key: 'closedWithoutConfirmedIssue',
      kind: 'uncertainty',
      message: `This ticket was ${ticket.status} without linking it to a confirmed product defect. Its recorded resolution may point to a different cause.`,
    })
  }

  if (!ticket.reportedVersion) {
    uncertainties.push({
      key: 'versionMissing',
      kind: 'uncertainty',
      message: 'The ticket does not record which product version the customer uses.',
    })
  } else if (ticket.reportedVersionPrecision === 'minor') {
    uncertainties.push({
      key: 'patchUnknown',
      kind: 'uncertainty',
      message: `The customer reported version '${ticket.reportedVersion}' without an exact patch number.`,
    })
  } else if (ticket.reportedVersionPrecision !== 'patch') {
    uncertainties.push({
      key: 'versionNotComparable',
      kind: 'uncertainty',
      message: `The reported version '${ticket.reportedVersion}' cannot be compared with release versions.`,
    })
  }

  if (ticket.unresolvedBugRefs.length > 0) {
    uncertainties.push({
      key: 'unknownIssueReferences',
      kind: 'uncertainty',
      message: `The ticket mentions issues that could not be found: ${joinWithAnd(ticket.unresolvedBugRefs.map(formatReference))}.`,
    })
  }

  return uncertainties
}

function collectCoverageNotes({ candidateBugs, counts }: TicketInvestigation): InvestigationLimitation[] {
  const coverageNotes: InvestigationLimitation[] = []

  if (counts.candidateBugLimitReached) {
    coverageNotes.push({
      key: 'candidateLimit',
      kind: 'coverage',
      message: `The list of potential related issues reached its limit of ${counts.candidateBugLimit}. More may exist.`,
    })
  }

  if (candidateBugs.some((bug) => bug.confirmedTicketsTruncated)) {
    coverageNotes.push({
      key: 'confirmedCasesTruncated',
      kind: 'coverage',
      message: 'Some issues list only part of their confirmed support cases.',
    })
  }

  if (counts.relatedTicketsTruncated) {
    coverageNotes.push({
      key: 'similarCasesTruncated',
      kind: 'coverage',
      message: `Showing the ${counts.relatedTickets} most recent of ${counts.relatedTicketTotal} similar support cases.`,
    })
  }

  return coverageNotes
}

export function collectInvestigationLimitations(
  investigation: TicketInvestigation,
): InvestigationLimitation[] {
  return [...collectUncertainties(investigation), ...collectCoverageNotes(investigation)]
}
