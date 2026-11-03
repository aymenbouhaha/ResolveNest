import { Bug, Code2, History, Tag } from 'lucide-react'
import type { ReactNode } from 'react'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import {
  ERROR_CODE_STATUS_LABELS,
  RELEASE_BOUND_CHECK_LABELS,
  RELEVANCE_TIER_LABELS,
  VERSION_ASSESSMENT_LABELS,
} from '../lib/evidenceLabels'
import { formatDate, humanizeToken, pluralize } from '../lib/formatters'
import { formatCustomerReference, formatReference, joinWithAnd } from '../lib/recordReferences'
import { presentRelevanceNote, type PresentedRelevanceNote } from '../lib/relevanceNoteText'
import type {
  CandidateBug,
  CandidateConfirmedTicket,
  RelevanceNote,
  ReleaseBoundCheck,
} from '../types/investigation'
import { EvidenceBadge } from './EvidenceBadge'
import { RelevanceNotes } from './RelevanceNotes'
import { ReleaseEvidenceList } from './ReleaseEvidenceList'
import { SupportingRecord } from './SupportingRecord'

const TICKET_LEVEL_NOTE_CODES = new Set([
  'ticketClosedWithoutConfirmedBug',
  'ticketHasConfirmedBug',
  'patchVersionUnknown',
])
const CHRONOLOGY_CONFLICT_NOTE_CODE = 'errorCodeMatchContradictedByIntroduction'
const OUTSIDE_RANGE_NOTE_CODE = 'versionOutsideDocumentedRange'

interface CandidateBugCardProps {
  bug: CandidateBug
  ticketStatus: string | null
  rank: number
  total: number
}

export function CandidateBugCard({ bug, ticketStatus, rank, total }: CandidateBugCardProps) {
  const { relevance } = bug
  const tier = RELEVANCE_TIER_LABELS[relevance.tier]
  const hasChronologyConflictNote = relevance.limitations.some(
    (note) => note.code === CHRONOLOGY_CONFLICT_NOTE_CODE,
  )
  const isRedundantNote = (note: RelevanceNote) =>
    TICKET_LEVEL_NOTE_CODES.has(note.code) ||
    (hasChronologyConflictNote && note.code === OUTSIDE_RANGE_NOTE_CODE)
  const presentIssueNotes = (notes: RelevanceNote[]): PresentedRelevanceNote[] =>
    notes
      .filter((note) => !isRedundantNote(note))
      .map((note) => presentRelevanceNote(bug, note, ticketStatus))

  return (
    <Card className="gap-0 py-0">
      <CardHeader className="gap-2 border-b py-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-muted-foreground tabular-nums">
            #{rank} of {total}
          </span>
          <Badge variant="outline" className="font-mono">
            <Bug aria-hidden="true" />
            {formatReference(bug.id)}
          </Badge>
          <EvidenceBadge evidence={bug.evidence} />
          <Badge variant="secondary">
            <Tag aria-hidden="true" />
            {tier.label}
          </Badge>
          {bug.severity ? <Badge variant="outline">Severity: {humanizeToken(bug.severity)}</Badge> : null}
          {bug.sourceStatus ? (
            <Badge variant="outline">Issue status: {humanizeToken(bug.sourceStatus)}</Badge>
          ) : null}
        </div>
        <h3 className="text-sm font-semibold text-foreground sm:text-base">
          {bug.title ?? 'Untitled issue'}
        </h3>
        <p className="text-sm text-muted-foreground">{tier.summary}</p>
      </CardHeader>

      <CardContent className="space-y-4 py-4">
        <dl className="grid gap-3 sm:grid-cols-3">
          <SignalTile icon={<Code2 className="size-3.5" aria-hidden="true" />} label="Error match">
            <span className="font-medium">{ERROR_CODE_STATUS_LABELS[relevance.errorCodes.status]}</span>
            <ErrorCodeList
              matched={relevance.errorCodes.matchedCodes}
              ticket={relevance.errorCodes.ticketCodes}
              bug={relevance.errorCodes.bugCodes}
            />
          </SignalTile>
          <SignalTile icon={<Tag className="size-3.5" aria-hidden="true" />} label="Version check">
            <span className="font-medium">{VERSION_ASSESSMENT_LABELS[relevance.version.assessment]}</span>
            <ReleaseBoundLine
              label="Introduced in"
              versions={relevance.version.introducedInVersions}
              check={relevance.version.introducedCheck}
            />
            <ReleaseBoundLine
              label="Fixed in"
              versions={relevance.version.fixedInVersions}
              check={relevance.version.fixedCheck}
            />
          </SignalTile>
          <SignalTile icon={<History className="size-3.5" aria-hidden="true" />} label="Other confirmed cases">
            {bug.confirmedTicketCount > 0 ? (
              <>
                <span className="font-medium">
                  {pluralize(bug.confirmedTicketCount, 'case')} · {pluralize(bug.confirmedCustomerCount, 'customer')}
                </span>
                <span className="block text-xs text-muted-foreground">Confirmed on other tickets</span>
              </>
            ) : (
              <span className="font-medium">None confirmed yet</span>
            )}
          </SignalTile>
        </dl>

        <RelevanceNotes
          supporting={presentIssueNotes(relevance.supporting)}
          limitations={presentIssueNotes(relevance.limitations)}
        />

        <Accordion type="multiple" className="rounded-lg border px-3">
          <AccordionItem value="details">
            <AccordionTrigger>Issue details and workaround</AccordionTrigger>
            <AccordionContent className="space-y-3">
              <p className="whitespace-pre-line">{bug.description ?? 'No description recorded.'}</p>
              <div className="rounded-md bg-muted/40 p-3">
                <p className="text-xs font-medium text-muted-foreground uppercase">Documented workaround</p>
                <p>{bug.workaround ?? 'No workaround recorded.'}</p>
              </div>
              <SupportingRecord sourceSystem={bug.sourceSystem} sourceId={bug.sourceId} />
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="releases">
            <AccordionTrigger>Release history</AccordionTrigger>
            <AccordionContent>
              <ReleaseEvidenceList introducedIn={bug.introducedIn} fixedIn={bug.fixedIn} />
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="confirmed-cases">
            <AccordionTrigger>Support cases confirmed with this issue ({bug.confirmedTicketCount})</AccordionTrigger>
            <AccordionContent>
              <ConfirmedCaseList bug={bug} />
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      </CardContent>
    </Card>
  )
}

interface ReleaseBoundLineProps {
  label: string
  versions: string[]
  check: ReleaseBoundCheck
}

function ReleaseBoundLine({ label, versions, check }: ReleaseBoundLineProps) {
  const text =
    versions.length > 0
      ? `${label} ${joinWithAnd(versions)}: ${RELEASE_BOUND_CHECK_LABELS[check]}`
      : `${label}: not recorded`
  return <span className="block text-xs text-muted-foreground">{text}</span>
}

interface SignalTileProps {
  icon: ReactNode
  label: string
  children: ReactNode
}

function SignalTile({ icon, label, children }: SignalTileProps) {
  return (
    <div className="min-w-0 space-y-1 rounded-lg border bg-muted/30 p-3">
      <dt className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
        {icon}
        {label}
      </dt>
      <dd className="space-y-1 text-sm">{children}</dd>
    </div>
  )
}

interface ErrorCodeListProps {
  matched: string[]
  ticket: string[]
  bug: string[]
}

function ErrorCodeList({ matched, ticket, bug }: ErrorCodeListProps) {
  if (matched.length > 0) {
    return (
      <span className="flex flex-wrap gap-1">
        {matched.map((code) => (
          <code key={code} className="rounded bg-background px-1.5 py-0.5 font-mono text-xs break-all ring-1 ring-border">
            {code}
          </code>
        ))}
      </span>
    )
  }
  return (
    <span className="block text-xs break-all text-muted-foreground">
      Ticket: {ticket.join(', ') || 'none'} · Issue: {bug.join(', ') || 'none'}
    </span>
  )
}

function ConfirmedCaseList({ bug }: { bug: CandidateBug }) {
  if (bug.confirmedTickets.length === 0) {
    return <p className="text-muted-foreground">No support case has been confirmed with this issue.</p>
  }

  return (
    <div className="space-y-2">
      <p className="text-xs text-muted-foreground">
        These cases belong to other tickets. They show the issue is real, not that it caused this ticket.
      </p>
      <ul className="divide-y rounded-md border">
        {bug.confirmedTickets.map((ticket) => (
          <ConfirmedCaseItem key={ticket.id} ticket={ticket} />
        ))}
      </ul>
      {bug.confirmedTicketsTruncated ? (
        <p className="text-xs text-muted-foreground">
          Showing {bug.confirmedTickets.length} of {bug.confirmedTicketCount} confirmed cases.
        </p>
      ) : null}
    </div>
  )
}

function ConfirmedCaseItem({ ticket }: { ticket: CandidateConfirmedTicket }) {
  const confirmedOn = formatDate(ticket.confirmedAt)
  return (
    <li className="flex flex-col gap-1 px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <p className="truncate text-sm">
          <span className="font-mono text-xs text-muted-foreground">{formatReference(ticket.id)}</span>{' '}
          {ticket.subject ?? 'Untitled ticket'}
        </p>
        <p className="text-xs text-muted-foreground">
          {formatCustomerReference(ticket.customerId)}
          {ticket.reportedVersion ? ` · version ${ticket.reportedVersion}` : ''}
          {ticket.status ? ` · ${humanizeToken(ticket.status)}` : ''}
          {confirmedOn ? ` · confirmed ${confirmedOn}` : ''}
          {ticket.confirmedBy ? ` by ${ticket.confirmedBy}` : ''}
        </p>
      </div>
      {ticket.sameCustomer ? (
        <Badge variant="secondary" className="shrink-0">
          Same customer
        </Badge>
      ) : null}
    </li>
  )
}
