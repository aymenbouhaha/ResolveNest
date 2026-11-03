import { FileText } from 'lucide-react'
import { cn } from '@/lib/utils'
import { describeSourceRecord } from '../lib/recordReferences'

interface SupportingRecordProps {
  sourceSystem: string | null
  sourceId: string | null
  className?: string
}

export function SupportingRecord({ sourceSystem, sourceId, className }: SupportingRecordProps) {
  const description = describeSourceRecord(sourceSystem, sourceId)

  if (!description) {
    return null
  }

  return (
    <p className={cn('flex min-w-0 items-center gap-1.5 text-xs text-muted-foreground', className)}>
      <FileText className="size-3.5 shrink-0" aria-hidden="true" />
      <span className="shrink-0">Source record</span>
      <span className="min-w-0 font-medium break-all text-foreground/80">{description}</span>
    </p>
  )
}
