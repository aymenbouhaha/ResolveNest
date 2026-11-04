import { Badge } from '@/components/ui/badge'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { EVIDENCE_CLASSIFICATION_LABELS } from '../lib/evidenceLabels'
import { EVIDENCE_STYLES } from '../lib/evidenceStyles'
import type { EvidenceClassification } from '../types/investigation'

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
        <Badge variant="outline" className={cn(evidenceClassName, className)}>
          <Icon aria-hidden="true" />
          {label}
        </Badge>
      </TooltipTrigger>
      <TooltipContent>{description}</TooltipContent>
    </Tooltip>
  )
}
