import { CircleCheck, CircleDashed, FileCheck2, Info, type LucideIcon } from 'lucide-react'
import type { EvidenceClassification } from '../types/investigation'

export const EVIDENCE_STYLES: Record<EvidenceClassification, { icon: LucideIcon; className: string }> = {
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
