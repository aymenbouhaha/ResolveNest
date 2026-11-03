import { Building2, Layers } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { formatDateTime, humanizeToken } from '../lib/formatters'
import { formatReference } from '../lib/recordReferences'
import type {
  InvestigatedTicket,
  ReportingCustomer,
  TicketFeature,
  TicketInvestigation,
} from '../types/investigation'
import { MetadataItem, NotRecorded } from './MetadataItem'
import { SupportingRecord } from './SupportingRecord'

interface TicketOverviewProps {
  investigation: TicketInvestigation
}

export function TicketOverview({ investigation }: TicketOverviewProps) {
  const { ticket, customer, features } = investigation
  const createdAt = formatDateTime(ticket.createdAt)
  const resolvedAt = formatDateTime(ticket.resolvedAt)

  return (
    <Card className="gap-0 py-0">
      <CardHeader className="gap-3 border-b py-5">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline" className="font-mono">
            {formatReference(ticket.id)}
          </Badge>
          {ticket.status ? <Badge variant="secondary">Status: {humanizeToken(ticket.status)}</Badge> : null}
          {ticket.priority ? (
            <Badge variant="secondary">Priority: {humanizeToken(ticket.priority)}</Badge>
          ) : null}
        </div>
        <h1 className="text-xl font-semibold tracking-tight text-balance text-foreground sm:text-2xl">
          {ticket.subject ?? 'Untitled ticket'}
        </h1>
      </CardHeader>

      <CardContent className="grid gap-6 py-5 lg:grid-cols-[minmax(0,1fr)_17rem]">
        <div className="min-w-0 space-y-4">
          <div className="space-y-1.5">
            <h2 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
              Reported problem
            </h2>
            {ticket.description ? (
              <p className="text-sm leading-relaxed whitespace-pre-line text-foreground">
                {ticket.description}
              </p>
            ) : (
              <p className="text-sm text-muted-foreground italic">No description recorded.</p>
            )}
          </div>

          {ticket.resolution ? (
            <div className="space-y-1.5 rounded-lg border bg-muted/40 p-3">
              <h2 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
                Recorded resolution
              </h2>
              <p className="text-sm whitespace-pre-line">{ticket.resolution}</p>
            </div>
          ) : null}

          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <MetadataItem label="Reported version">
              <ReportedVersion ticket={ticket} />
            </MetadataItem>
            <MetadataItem label="Opened">{createdAt ?? <NotRecorded />}</MetadataItem>
            <MetadataItem label="Resolved">{resolvedAt ?? <NotRecorded />}</MetadataItem>
          </dl>

          <SupportingRecord sourceSystem={ticket.sourceSystem} sourceId={ticket.sourceId} />
        </div>

        <aside className="space-y-4 lg:border-l lg:pl-6" aria-label="Customer and product area">
          <CustomerPanel customer={customer} />
          <Separator />
          <FeaturePanel features={features} />
        </aside>
      </CardContent>
    </Card>
  )
}

function ReportedVersion({ ticket }: { ticket: InvestigatedTicket }) {
  if (!ticket.reportedVersion) {
    return <NotRecorded />
  }

  return (
    <span className="space-y-0.5">
      <span className="block font-mono">{ticket.reportedVersion}</span>
      {ticket.reportedVersionPrecision === 'minor' ? (
        <span className="block text-xs text-muted-foreground">Exact patch not specified</span>
      ) : null}
    </span>
  )
}

function CustomerPanel({ customer }: { customer: ReportingCustomer | null }) {
  return (
    <div className="space-y-2">
      <h2 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
        <Building2 className="size-3.5" aria-hidden="true" />
        Customer
      </h2>
      {customer ? (
        <div className="space-y-2">
          <p className="font-medium">{customer.name ?? formatReference(customer.id)}</p>
          <div className="flex flex-wrap gap-1.5">
            {customer.plan ? <Badge variant="secondary">Plan: {humanizeToken(customer.plan)}</Badge> : null}
            {customer.deployment ? (
              <Badge variant="secondary">Deployment: {humanizeToken(customer.deployment)}</Badge>
            ) : null}
          </div>
          <SupportingRecord sourceSystem={customer.sourceSystem} sourceId={customer.sourceId} />
        </div>
      ) : (
        <p className="text-sm text-muted-foreground italic">
          No customer is recorded for this ticket.
        </p>
      )}
    </div>
  )
}

function FeaturePanel({ features }: { features: TicketFeature[] }) {
  return (
    <div className="space-y-2">
      <h2 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
        <Layers className="size-3.5" aria-hidden="true" />
        Product area
      </h2>
      {features.length > 0 ? (
        <ul className="flex flex-wrap gap-1.5">
          {features.map((feature) => (
            <li key={feature.id}>
              <Badge variant="outline">{feature.name ?? formatReference(feature.id)}</Badge>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground italic">No product feature is recorded for this ticket.</p>
      )}
    </div>
  )
}
