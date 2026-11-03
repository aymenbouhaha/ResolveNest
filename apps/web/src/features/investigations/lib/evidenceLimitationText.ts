import type { TicketInvestigation } from '../types/investigation'
import type { ContextLimitation } from '../types/replyDraft'
import { formatIdentifiersForDisplay } from './answerTextFormatting'
import type { EntityNameLookup } from './citationLabels'
import { formatReference, joinWithAnd } from './recordReferences'

type LimitationTextBuilder = (subjects: string[], investigation: TicketInvestigation) => string

function referencesWithPrefix(subjectIds: string[], prefix: string): string[] {
  return subjectIds.filter((id) => id.startsWith(prefix)).map(formatReference)
}

function quotedReportedVersion(investigation: TicketInvestigation): string {
  return investigation.ticket.reportedVersion ? `'${investigation.ticket.reportedVersion}'` : 'a version'
}

const LIMITATION_TEXT_BUILDERS: Record<string, LimitationTextBuilder> = {
  noConfirmedBug: () =>
    'No product issue has been confirmed for this ticket. Potential issues share only a product feature with it.',
  reportedVersionIsMinorLine: (_subjects, investigation) =>
    `The customer reported version ${quotedReportedVersion(investigation)} without an exact patch number.`,
  reportedVersionUnknown: () => 'The ticket does not record a product version that can be compared with releases.',
  documentedFixIsNotCustomerResolution: (subjects) =>
    `Fixes are documented for ${joinWithAnd(subjects.map(formatReference))}. That does not show the customer runs a fixed release or that their problem is solved.`,
  closedWithoutConfirmedBug: (_subjects, investigation) =>
    `This ticket was ${investigation.ticket.status ?? 'closed'} without linking it to a confirmed product defect. Its recorded resolution may point to a different cause.`,
  conflictingCandidateSignals: (subjects) =>
    subjects.length === 1
      ? `${formatReference(subjects[0])} shares an error code with this ticket, but the reported version is older than the release that introduced it.`
      : `${joinWithAnd(subjects.map(formatReference))} share an error code with this ticket, but the reported version is older than the releases that introduced them.`,
  noUncontradictedErrorCodeMatch: () =>
    'No potential issue shares an error code with this ticket without a release-date conflict, so no single likely defect stands out.',
  uncorroboratedCandidates: (subjects) =>
    subjects.length === 1
      ? `${formatReference(subjects[0])} has no confirmed cases and no release history; only the product feature links it to this ticket.`
      : `${joinWithAnd(subjects.map(formatReference))} have no confirmed cases and no release history; only the product feature links them to this ticket.`,
  historicalConfirmationsAreOtherTickets: () =>
    'Confirmed cases listed for potential issues belong to other tickets, not to this one.',
  knowledgeDocumentsCandidateBugs: (subjects) =>
    `${joinWithAnd(referencesWithPrefix(subjects, 'kb:'))} describe${referencesWithPrefix(subjects, 'kb:').length === 1 ? 's' : ''} potential issues (${joinWithAnd(referencesWithPrefix(subjects, 'bug:'))}). That does not show those issues caused this ticket.`,
  knowledgeContentNotLoaded: () => 'Only the titles and summaries of troubleshooting guides were available.',
  unresolvedBugReferences: (subjects) =>
    `The ticket mentions issues that could not be found: ${joinWithAnd(subjects.map(formatReference))}.`,
  relevanceIsHeuristic: () =>
    'How closely each potential issue matches is an automated comparison, not a diagnosis.',
}

export function describeEvidenceLimitation(
  limitation: ContextLimitation,
  investigation: TicketInvestigation,
  names: EntityNameLookup,
): string {
  const buildText = LIMITATION_TEXT_BUILDERS[limitation.code]
  return buildText
    ? buildText(limitation.subjectIds, investigation)
    : formatIdentifiersForDisplay(limitation.message, names)
}
