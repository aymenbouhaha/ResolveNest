import { RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { CandidateBugsSection } from './components/CandidateBugsSection'
import { ConfirmedBugsSection } from './components/ConfirmedBugsSection'
import { InvestigationErrorState, InvestigationSkeleton } from './components/InvestigationStates'
import { InvestigationSummary } from './components/InvestigationSummary'
import { KnowledgeSection } from './components/KnowledgeSection'
import { RelatedTicketsSection } from './components/RelatedTicketsSection'
import { ReleaseEvidenceSection } from './components/ReleaseEvidenceSection'
import { TicketOverview } from './components/TicketOverview'
import { useInvestigation } from './hooks/useInvestigation'
import { formatReference } from './lib/recordReferences'
import type { TicketInvestigation } from './types/investigation'

interface InvestigationWorkspaceProps {
  ticketId: string
  scenarioLabel: string | null
}

export function InvestigationWorkspace({ ticketId, scenarioLabel }: InvestigationWorkspaceProps) {
  const { state, reload } = useInvestigation(ticketId)
  const isLoading = state.status === 'loading'

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Investigation
          </p>
          <p className="truncate text-sm text-foreground">
            <span className="font-mono">{formatReference(ticketId)}</span>
            {scenarioLabel ? <span className="text-muted-foreground"> · {scenarioLabel}</span> : null}
          </p>
        </div>
        <Button variant="outline" onClick={reload} disabled={isLoading}>
          <RefreshCw className={isLoading ? 'animate-spin' : undefined} aria-hidden="true" />
          {isLoading ? 'Loading' : 'Refresh'}
        </Button>
      </div>

      {state.status === 'loading' ? <InvestigationSkeleton /> : null}
      {state.status === 'error' ? (
        <InvestigationErrorState ticketId={ticketId} error={state.error} onRetry={reload} />
      ) : null}
      {state.status === 'success' ? (
        <InvestigationResult key={state.investigation.ticket.id} investigation={state.investigation} />
      ) : null}
    </div>
  )
}

function InvestigationResult({ investigation }: { investigation: TicketInvestigation }) {
  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
      <div className="min-w-0 space-y-8">
        <TicketOverview investigation={investigation} />
        <div className="xl:hidden">
          <InvestigationSummary investigation={investigation} />
        </div>
        <ConfirmedBugsSection bugs={investigation.confirmedBugs} />
        <CandidateBugsSection
          bugs={investigation.candidateBugs}
          ticket={investigation.ticket}
          hasConfirmedIssue={investigation.confirmedBugs.length > 0}
        />
        <ReleaseEvidenceSection investigation={investigation} />
        <KnowledgeSection articles={investigation.knowledgeArticles} features={investigation.features} />
        <RelatedTicketsSection tickets={investigation.relatedTickets} counts={investigation.counts} />
      </div>
      <aside className="hidden xl:block" aria-label="Investigation summary">
        <div className="sticky top-20">
          <InvestigationSummary investigation={investigation} />
        </div>
      </aside>
    </div>
  )
}
