import { CircleAlert, ListChecks, Scale } from 'lucide-react'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { EVIDENCE_CLASSIFICATION_LABELS } from '../lib/evidenceLabels'
import {
  collectInvestigationLimitations,
  type InvestigationLimitation,
} from '../lib/investigationLimitations'
import type { EvidenceClassification, TicketInvestigation } from '../types/investigation'
import { EvidenceBadge } from './EvidenceBadge'

interface InvestigationSummaryProps {
  investigation: TicketInvestigation
}

const EVIDENCE_ORDER: EvidenceClassification[] = ['confirmed', 'documented', 'candidate', 'context']

export function InvestigationSummary({ investigation }: InvestigationSummaryProps) {
  const { counts } = investigation
  const limitations = collectInvestigationLimitations(investigation)
  const uncertainties = limitations.filter((limitation) => limitation.kind === 'uncertainty')
  const coverageNotes = limitations.filter((limitation) => limitation.kind === 'coverage')
  const similarCaseValue = counts.relatedTicketsTruncated
    ? `${counts.relatedTickets} of ${counts.relatedTicketTotal}`
    : String(counts.relatedTickets)

  const overviewTotals = [
    { label: 'Confirmed issues', value: String(counts.confirmedBugs), href: '#confirmed-issues' },
    { label: 'Potential issues', value: String(counts.candidateBugs), href: '#potential-issues' },
    { label: 'Troubleshooting guides', value: String(counts.knowledgeArticles), href: '#troubleshooting-guides' },
    { label: 'Similar cases', value: similarCaseValue, href: '#similar-cases' },
  ]

  return (
    <div className="space-y-4">
      <Card size="sm">
        <CardHeader>
          <h2 data-slot="card-title" className="flex items-center gap-2 font-heading leading-snug text-sm font-semibold">
            <ListChecks className="size-4 text-primary" aria-hidden="true" />
            At a glance
          </h2>
        </CardHeader>
        <CardContent>
          <ul className="grid grid-cols-2 gap-2">
            {overviewTotals.map((total) => (
              <li key={total.label}>
                <a
                  href={total.href}
                  className="block rounded-lg border bg-muted/30 px-3 py-2 transition-colors outline-none hover:bg-accent focus-visible:ring-3 focus-visible:ring-ring/50"
                >
                  <span className="block text-xs text-muted-foreground">{total.label}</span>
                  <span className="block text-lg font-semibold tabular-nums">{total.value}</span>
                </a>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <Card size="sm">
        <CardHeader>
          <h2 data-slot="card-title" className="flex items-center gap-2 font-heading leading-snug text-sm font-semibold">
            <CircleAlert className="size-4 text-evidence-candidate" aria-hidden="true" />
            What remains uncertain
          </h2>
        </CardHeader>
        <CardContent className="space-y-4">
          {uncertainties.length > 0 ? (
            <LimitationList limitations={uncertainties} />
          ) : (
            <p className="text-sm text-muted-foreground">
              No open questions were identified from the available information.
            </p>
          )}
          {coverageNotes.length > 0 ? (
            <div className="space-y-2 border-t pt-3">
              <h3 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
                Investigation coverage
              </h3>
              <LimitationList limitations={coverageNotes} muted />
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card size="sm">
        <CardHeader>
          <h2 data-slot="card-title" className="flex items-center gap-2 font-heading leading-snug text-sm font-semibold">
            <Scale className="size-4 text-primary" aria-hidden="true" />
            How to read the labels
          </h2>
        </CardHeader>
        <CardContent>
          <ul className="space-y-2.5">
            {EVIDENCE_ORDER.map((evidence) => (
              <li key={evidence} className="space-y-1">
                <EvidenceBadge evidence={evidence} />
                <p className="text-xs text-muted-foreground">
                  {EVIDENCE_CLASSIFICATION_LABELS[evidence].description}
                </p>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  )
}

interface LimitationListProps {
  limitations: InvestigationLimitation[]
  muted?: boolean
}

function LimitationList({ limitations, muted = false }: LimitationListProps) {
  return (
    <ul className="space-y-2">
      {limitations.map((limitation) => (
        <li
          key={limitation.key}
          className={muted ? 'flex gap-2 text-xs text-muted-foreground' : 'flex gap-2 text-sm'}
        >
          <span
            className={
              muted
                ? 'mt-1.5 size-1.5 shrink-0 rounded-full bg-evidence-context'
                : 'mt-1.5 size-1.5 shrink-0 rounded-full bg-evidence-candidate'
            }
            aria-hidden="true"
          />
          <span>{limitation.message}</span>
        </li>
      ))}
    </ul>
  )
}
