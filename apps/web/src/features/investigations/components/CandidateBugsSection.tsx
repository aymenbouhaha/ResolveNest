import { SearchCheck } from 'lucide-react'
import { pluralize } from '../lib/formatters'
import type { CandidateBug, InvestigatedTicket } from '../types/investigation'
import { CandidateBugCard } from './CandidateBugCard'
import { EmptyEvidence, EvidenceSection } from './EvidenceSection'

interface CandidateBugsSectionProps {
  bugs: CandidateBug[]
  ticket: InvestigatedTicket
  hasConfirmedIssue: boolean
}

const SECTION_DESCRIPTION =
  'Known product issues that affect the same feature as this ticket, most closely matching first. None of them has been confirmed for this ticket.'
const SECTION_DESCRIPTION_WITH_CONFIRMED_ISSUE =
  'Other known product issues that affect the same feature. This ticket already has a confirmed issue, so these are shown for comparison only.'

export function CandidateBugsSection({ bugs, ticket, hasConfirmedIssue }: CandidateBugsSectionProps) {
  return (
    <EvidenceSection
      id="potential-issues"
      icon={SearchCheck}
      title="Potential related issues"
      description={hasConfirmedIssue ? SECTION_DESCRIPTION_WITH_CONFIRMED_ISSUE : SECTION_DESCRIPTION}
      meta={bugs.length > 0 ? pluralize(bugs.length, 'issue') : null}
    >
      {bugs.length > 0 ? (
        <div className="space-y-3">
          {bugs.map((bug, index) => (
            <CandidateBugCard
              key={bug.id}
              bug={bug}
              ticketStatus={ticket.status}
              rank={index + 1}
              total={bugs.length}
            />
          ))}
        </div>
      ) : (
        <EmptyEvidence>No known product issue affects the same feature as this ticket.</EmptyEvidence>
      )}
    </EvidenceSection>
  )
}
