import { ChevronDown, History } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatDate, humanizeToken } from '../lib/formatters'
import { formatCustomerReference, formatReference } from '../lib/recordReferences'
import type { InvestigationResultCounts, RelatedTicket } from '../types/investigation'
import { EmptyEvidence, EvidenceSection } from './EvidenceSection'

const INITIALLY_VISIBLE_CASES = 8

interface RelatedTicketsSectionProps {
  tickets: RelatedTicket[]
  counts: InvestigationResultCounts
}

export function RelatedTicketsSection({ tickets, counts }: RelatedTicketsSectionProps) {
  const [showAll, setShowAll] = useState(false)
  const visibleTickets = showAll ? tickets : tickets.slice(0, INITIALLY_VISIBLE_CASES)
  const hiddenCount = tickets.length - visibleTickets.length
  const meta = counts.relatedTicketsTruncated
    ? `${tickets.length} most recent of ${counts.relatedTicketTotal}`
    : `${tickets.length} cases`

  return (
    <EvidenceSection
      id="similar-cases"
      icon={History}
      title="Similar support cases"
      description="Other tickets about the same product feature, newest first. They are background for comparison and do not indicate this ticket's cause. Issue IDs shown were confirmed on those tickets, not on this one."
      meta={tickets.length > 0 ? meta : null}
    >
      {tickets.length > 0 ? (
        <Card size="sm" className="py-0">
          <CardContent className="px-0">
            <ul className="divide-y">
              {visibleTickets.map((ticket) => (
                <SimilarCaseRow key={ticket.id} ticket={ticket} />
              ))}
            </ul>
            {hiddenCount > 0 ? (
              <div className="border-t p-2">
                <Button variant="ghost" className="w-full" onClick={() => setShowAll(true)}>
                  <ChevronDown aria-hidden="true" />
                  Show {hiddenCount} more
                </Button>
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : (
        <EmptyEvidence>No other support case concerns the same product feature.</EmptyEvidence>
      )}
    </EvidenceSection>
  )
}

function SimilarCaseRow({ ticket }: { ticket: RelatedTicket }) {
  const openedOn = formatDate(ticket.createdAt)

  return (
    <li className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0 space-y-1">
        <p className="text-sm">
          <span className="font-mono text-xs text-muted-foreground">{formatReference(ticket.id)}</span>{' '}
          <span className="font-medium">{ticket.subject ?? 'Untitled ticket'}</span>
        </p>
        <p className="text-xs text-muted-foreground">
          {formatCustomerReference(ticket.customerId)}
          {ticket.reportedVersion ? ` · version ${ticket.reportedVersion}` : ''}
          {ticket.status ? ` · ${humanizeToken(ticket.status)}` : ''}
          {openedOn ? ` · opened ${openedOn}` : ''}
        </p>
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-1.5">
        {ticket.sameCustomer ? <Badge variant="secondary">Same customer</Badge> : null}
        {ticket.confirmedBugIds.map((bugId) => (
          <Badge key={bugId} variant="outline">
            Confirmed: <span className="font-mono">{formatReference(bugId)}</span>
          </Badge>
        ))}
      </div>
    </li>
  )
}
