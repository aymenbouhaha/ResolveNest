import { Bug, ShieldCheck } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { formatDateTime, humanizeToken } from '../lib/formatters'
import { formatReference, joinWithAnd } from '../lib/recordReferences'
import type { ConfirmedBug } from '../types/investigation'
import { EmptyEvidence, EvidenceSection } from './EvidenceSection'
import { EvidenceBadge } from './EvidenceBadge'
import { MetadataItem, NotRecorded } from './MetadataItem'
import { ReleaseEvidenceList } from './ReleaseEvidenceList'
import { SupportingRecord } from './SupportingRecord'

interface ConfirmedBugsSectionProps {
  bugs: ConfirmedBug[]
}

export function ConfirmedBugsSection({ bugs }: ConfirmedBugsSectionProps) {
  return (
    <EvidenceSection
      id="confirmed-issues"
      icon={ShieldCheck}
      title="Confirmed issues"
      description="Product issues that a support engineer has confirmed for this ticket."
    >
      {bugs.length > 0 ? (
        <div className="space-y-3">
          {bugs.map((bug) => (
            <ConfirmedBugCard key={bug.id} bug={bug} />
          ))}
        </div>
      ) : (
        <EmptyEvidence>
          No confirmed product issue has been linked to this ticket.
        </EmptyEvidence>
      )}
    </EvidenceSection>
  )
}

function ConfirmedBugCard({ bug }: { bug: ConfirmedBug }) {
  const affectedFeatureNames = bug.affectedFeatures.map(
    (feature) => feature.name ?? formatReference(feature.id),
  )

  return (
    <Card className="gap-0 border-l-4 border-l-evidence-confirmed py-0">
      <CardHeader className="gap-2 border-b py-4">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline" className="font-mono">
            <Bug aria-hidden="true" />
            {formatReference(bug.id)}
          </Badge>
          <EvidenceBadge evidence={bug.evidence} />
          {bug.severity ? <Badge variant="outline">Severity: {humanizeToken(bug.severity)}</Badge> : null}
          {bug.sourceStatus ? (
            <Badge variant="outline">Issue status: {humanizeToken(bug.sourceStatus)}</Badge>
          ) : null}
        </div>
        <h3 className="text-sm font-semibold sm:text-base">{bug.title ?? 'Untitled issue'}</h3>
      </CardHeader>
      <CardContent className="space-y-4 py-4">
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <MetadataItem label="Confirmed by">{bug.confirmedBy ?? <NotRecorded />}</MetadataItem>
          <MetadataItem label="Confirmed on">{formatDateTime(bug.confirmedAt) ?? <NotRecorded />}</MetadataItem>
          <MetadataItem label="Affected features">
            {affectedFeatureNames.length > 0 ? joinWithAnd(affectedFeatureNames) : <NotRecorded />}
          </MetadataItem>
        </dl>
        <div className="rounded-md bg-muted/40 p-3 text-sm">
          <p className="text-xs font-medium text-muted-foreground uppercase">Documented workaround</p>
          <p>{bug.workaround ?? 'No workaround recorded.'}</p>
        </div>
        <div className="space-y-2">
          <h4 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Release history</h4>
          <ReleaseEvidenceList introducedIn={bug.introducedIn} fixedIn={bug.fixedIn} />
        </div>
        <SupportingRecord sourceSystem={bug.sourceSystem} sourceId={bug.sourceId} />
      </CardContent>
    </Card>
  )
}
