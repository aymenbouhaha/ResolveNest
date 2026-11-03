import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

interface EvidenceSectionProps {
  id: string
  icon: LucideIcon
  title: string
  description: ReactNode
  meta?: ReactNode
  children: ReactNode
}

export function EvidenceSection({
  id,
  icon: Icon,
  title,
  description,
  meta,
  children,
}: EvidenceSectionProps) {
  const headingId = `${id}-heading`

  return (
    <section id={id} aria-labelledby={headingId} className="scroll-mt-20 space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-x-4 gap-y-1">
        <div className="min-w-0 space-y-1">
          <h2
            id={headingId}
            className="flex items-center gap-2 text-base font-semibold tracking-tight text-foreground"
          >
            <Icon className="size-4 text-primary" aria-hidden="true" />
            {title}
          </h2>
          <p className="max-w-3xl text-sm text-muted-foreground">{description}</p>
        </div>
        {meta ? <div className="text-xs text-muted-foreground">{meta}</div> : null}
      </div>
      {children}
    </section>
  )
}

interface EmptyEvidenceProps {
  children: ReactNode
}

export function EmptyEvidence({ children }: EmptyEvidenceProps) {
  return (
    <div className="rounded-xl border border-dashed bg-card/60 px-4 py-6 text-center text-sm text-muted-foreground">
      {children}
    </div>
  )
}
