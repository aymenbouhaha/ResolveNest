export interface DemoTicket {
  ticketId: string
  label: string
  scenario: string
}

export const DEMO_TICKETS: readonly DemoTicket[] = [
  {
    ticketId: 'ticket:TCK-1046',
    label: 'Acme CSV export regression',
    scenario: 'CSV export after 2.3',
  },
  {
    ticketId: 'ticket:TCK-1006',
    label: 'HTTP 403 permissions issue',
    scenario: 'API access denied',
  },
  {
    ticketId: 'ticket:TCK-1043',
    label: 'Webhook delays',
    scenario: 'Delayed webhook delivery',
  },
  {
    ticketId: 'ticket:TCK-1017',
    label: 'Confirmed CSV export defect',
    scenario: 'Linked to BUG-231',
  },
]

export const DEFAULT_TICKET_ID = DEMO_TICKETS[0].ticketId

export function shortTicketId(ticketId: string): string {
  return ticketId.replace(/^ticket:/, '')
}
