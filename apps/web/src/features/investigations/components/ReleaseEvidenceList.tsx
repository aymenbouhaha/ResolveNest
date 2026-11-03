import { PackageCheck, PackagePlus } from 'lucide-react'
import { formatDate } from '../lib/formatters'
import type { ReleaseEvidence } from '../types/investigation'
import { EvidenceBadge } from './EvidenceBadge'

type ReleaseRole = 'introduced' | 'fixed'

const RELEASE_ROLE_LABELS: Record<ReleaseRole, string> = {
  introduced: 'Introduced in version',
  fixed: 'Fixed in version',
}

interface ReleaseEvidenceListProps {
  introducedIn: ReleaseEvidence[]
  fixedIn: ReleaseEvidence[]
}

export function ReleaseEvidenceList({ introducedIn, fixedIn }: ReleaseEvidenceListProps) {
  const entries = [
    ...introducedIn.map((release) => ({ role: 'introduced' as const, release })),
    ...fixedIn.map((release) => ({ role: 'fixed' as const, release })),
  ]

  if (entries.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        No affected or fixed release is recorded for this issue.
      </p>
    )
  }

  return (
    <ul className="space-y-2">
      {entries.map(({ role, release }) => (
        <ReleaseEvidenceItem key={`${role}-${release.id}`} role={role} release={release} />
      ))}
    </ul>
  )
}

interface ReleaseEvidenceItemProps {
  role: ReleaseRole
  release: ReleaseEvidence
}

function ReleaseEvidenceItem({ role, release }: ReleaseEvidenceItemProps) {
  const Icon = role === 'fixed' ? PackageCheck : PackagePlus
  const releasedOn = formatDate(release.releasedAt)

  return (
    <li className="space-y-1.5 rounded-lg border bg-muted/30 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Icon className="size-4 text-muted-foreground" aria-hidden="true" />
        <span className="text-sm text-muted-foreground">{RELEASE_ROLE_LABELS[role]}</span>
        <span className="font-mono text-sm font-semibold">{release.version ?? 'unknown'}</span>
        {releasedOn ? (
          <span className="text-xs text-muted-foreground">released {releasedOn}</span>
        ) : null}
        <EvidenceBadge evidence={release.evidence} />
      </div>
      {release.evidenceText ? (
        <p className="text-sm text-foreground/90">{release.evidenceText}</p>
      ) : null}
    </li>
  )
}
