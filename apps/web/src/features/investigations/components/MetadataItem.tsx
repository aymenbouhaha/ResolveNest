import type { ReactNode } from 'react'

interface MetadataItemProps {
  label: string
  children: ReactNode
}

export function MetadataItem({ label, children }: MetadataItemProps) {
  return (
    <div className="min-w-0 space-y-0.5">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="text-sm text-foreground">{children}</dd>
    </div>
  )
}

export function NotRecorded() {
  return <span className="text-muted-foreground italic">Not recorded</span>
}
