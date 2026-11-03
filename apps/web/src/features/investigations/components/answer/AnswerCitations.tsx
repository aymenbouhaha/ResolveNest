import { FileText, Link2 } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import type { CitationPresentation } from '../../lib/citationLabels'
import { EvidenceBadge } from '../EvidenceBadge'

interface CitationChipsProps {
  citationIds: string[]
  citations: ReadonlyMap<string, CitationPresentation>
}

export function CitationChips({ citationIds, citations }: CitationChipsProps) {
  const resolved = citationIds
    .map((citationId) => citations.get(citationId))
    .filter((citation): citation is CitationPresentation => citation !== undefined)

  if (resolved.length === 0) {
    return null
  }

  return (
    <ul className="mt-1.5 flex flex-wrap gap-1" aria-label="Supporting records for this statement">
      {resolved.map((citation) => (
        <li key={citation.citationId}>
          <Badge
            variant="outline"
            className="h-auto max-w-full py-0.5 font-normal whitespace-normal text-muted-foreground"
          >
            {citation.isConnection ? <Link2 aria-hidden="true" /> : <FileText aria-hidden="true" />}
            {citation.label}
          </Badge>
        </li>
      ))}
    </ul>
  )
}

interface SupportingRecordsListProps {
  citations: CitationPresentation[]
}

export function SupportingRecordsList({ citations }: SupportingRecordsListProps) {
  if (citations.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        This answer does not cite any ticket records. Treat it as unsupported until you check the
        records above.
      </p>
    )
  }

  const records = citations.filter((citation) => !citation.isConnection)
  const connections = citations.filter((citation) => citation.isConnection)

  return (
    <div className="space-y-3">
      <p className="text-xs text-muted-foreground">
        The answer cites these ticket records. A citation shows where a statement comes from, not
        that the statement is correct.
      </p>
      {records.length > 0 ? (
        <ul className="flex flex-wrap gap-1.5" aria-label="Cited records">
          {records.map((citation) => (
            <li key={citation.citationId}>
              <Badge variant="outline" className="h-auto py-0.5 whitespace-normal">
                <FileText aria-hidden="true" />
                {citation.label}
              </Badge>
            </li>
          ))}
        </ul>
      ) : null}
      {connections.length > 0 ? (
        <ul className="space-y-1.5" aria-label="Cited connections">
          {connections.map((citation) => (
            <li key={citation.citationId} className="flex flex-wrap items-center gap-2 text-sm">
              <Link2 className="size-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
              <span>{citation.label}</span>
              {citation.evidence ? <EvidenceBadge evidence={citation.evidence} /> : null}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
