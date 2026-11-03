import { ArrowRight, GitBranch } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { formatDate } from '../lib/formatters'
import { formatReference } from '../lib/recordReferences'
import type {
  EvidenceClassification,
  ReleaseEvidence,
  TicketInvestigation,
} from '../types/investigation'
import { EmptyEvidence, EvidenceSection } from './EvidenceSection'
import { EvidenceBadge } from './EvidenceBadge'

interface IssueReleaseHistory {
  issueId: string
  issueTitle: string | null
  issueLink: EvidenceClassification
  introducedIn: ReleaseEvidence[]
  fixedIn: ReleaseEvidence[]
}

function collectReleaseHistories({
  confirmedBugs,
  candidateBugs,
}: TicketInvestigation): IssueReleaseHistory[] {
  return [...confirmedBugs, ...candidateBugs]
    .filter((bug) => bug.introducedIn.length > 0 || bug.fixedIn.length > 0)
    .map((bug) => ({
      issueId: bug.id,
      issueTitle: bug.title,
      issueLink: bug.evidence,
      introducedIn: bug.introducedIn,
      fixedIn: bug.fixedIn,
    }))
}

interface ReleaseEvidenceSectionProps {
  investigation: TicketInvestigation
}

export function ReleaseEvidenceSection({ investigation }: ReleaseEvidenceSectionProps) {
  const histories = collectReleaseHistories(investigation)

  return (
    <EvidenceSection
      id="release-history"
      icon={GitBranch}
      title="Release history"
      description="Versions in which the issues above were introduced and fixed. A fix in a release does not mean this customer has installed it or that the ticket is resolved."
    >
      {histories.length > 0 ? (
        <Card size="sm" className="py-0">
          <CardContent className="px-0">
            <ul className="divide-y">
              {histories.map((history) => (
                <ReleaseHistoryRow key={history.issueId} history={history} />
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : (
        <EmptyEvidence>No release history is recorded for the issues in this investigation.</EmptyEvidence>
      )}
    </EvidenceSection>
  )
}

function ReleaseHistoryRow({ history }: { history: IssueReleaseHistory }) {
  return (
    <li className="grid gap-2 px-4 py-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-sm font-semibold">{formatReference(history.issueId)}</span>
          <EvidenceBadge evidence={history.issueLink} />
        </div>
        <p className="truncate text-sm text-muted-foreground">{history.issueTitle}</p>
      </div>
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <ReleaseChip label="Introduced in" releases={history.introducedIn} />
        <ArrowRight className="size-4 text-muted-foreground" aria-hidden="true" />
        <ReleaseChip label="Fixed in" releases={history.fixedIn} />
      </div>
    </li>
  )
}

function ReleaseChip({ label, releases }: { label: string; releases: ReleaseEvidence[] }) {
  if (releases.length === 0) {
    return (
      <span className="rounded-md border border-dashed px-2 py-1 text-xs text-muted-foreground">
        {label === 'Fixed in' ? 'No fix recorded' : 'Introduction not recorded'}
      </span>
    )
  }

  return (
    <span className="flex flex-wrap gap-1">
      {releases.map((release) => {
        const releasedOn = formatDate(release.releasedAt)
        return (
          <span key={release.id} className="rounded-md border bg-muted/40 px-2 py-1 text-xs">
            {label} <span className="font-mono font-semibold">{release.version ?? 'unknown'}</span>
            {releasedOn ? <span className="text-muted-foreground"> · {releasedOn}</span> : null}
          </span>
        )
      })}
    </span>
  )
}
