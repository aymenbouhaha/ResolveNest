import { CircleCheck, CircleDashed, FileCheck2, Info, type LucideIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { EVIDENCE_CLASSIFICATION_LABELS } from '../lib/evidenceLabels'
import type { EvidenceClassification } from '../types/investigation'

const EVIDENCE_STYLES: Record<EvidenceClassification, { icon: LucideIcon; className: string }> = {
  confirmed: {
    icon: CircleCheck,
    className:
      'border-evidence-confirmed-border bg-evidence-confirmed-surface text-evidence-confirmed',
  },
  documented: {
    icon: FileCheck2,
    className:
      'border-evidence-documented-border bg-evidence-documented-surface text-evidence-documented',
  },
  candidate: {
    icon: CircleDashed,
    className:
      'border-evidence-candidate-border bg-evidence-candidate-surface text-evidence-candidate',
  },
  context: {
    icon: Info,
    className: 'border-evidence-context-border bg-evidence-context-surface text-evidence-context',
  },
}

interface EvidenceBadgeProps {
  evidence: EvidenceClassification
  className?: string
}

export function EvidenceBadge({ evidence, className }: EvidenceBadgeProps) {
  const { icon: Icon, className: evidenceClassName } = EVIDENCE_STYLES[evidence]
  const { label, description } = EVIDENCE_CLASSIFICATION_LABELS[evidence]

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Badge
          variant="outline"
          tabIndex={0}
          aria-label={`${label}: ${description}`}
          className={cn(evidenceClassName, className)}
        >
          <Icon aria-hidden="true" />
          {label}
        </Badge>
      </TooltipTrigger>
      <TooltipContent>{description}</TooltipContent>
    </Tooltip>
  )
}
