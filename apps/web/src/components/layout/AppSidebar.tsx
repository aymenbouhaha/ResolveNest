import { Database, LayoutDashboard, Ticket, Waypoints } from 'lucide-react'
import { useId } from 'react'
import { cn } from '@/lib/utils'
import { shortTicketId } from '@/features/tickets/ticketsApi'
import type { TicketList } from '@/features/tickets/useTickets'
import { Button } from '@/components/ui/button'

interface AppSidebarProps {
  selectedTicketId: string | null
  tickets: TicketList
  onSelectTicket: (ticketId: string) => void
  isNavigationLocked: boolean
}

export function AppSidebar({ selectedTicketId, onSelectTicket, isNavigationLocked, tickets }: AppSidebarProps) {
  const searchId = useId()
  const ticketsLabelId = useId()
  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <div className="flex h-16 items-center gap-2.5 border-b border-sidebar-border px-5">
        <span className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <Waypoints className="size-4" aria-hidden="true" />
        </span>
        <div className="leading-tight">
          <p className="text-sm font-semibold tracking-tight text-foreground">ResolveNest</p>
          <p className="text-xs text-muted-foreground">Support investigations</p>
        </div>
      </div>

      <nav aria-label="Primary" className="flex-1 space-y-6 overflow-y-auto px-3 py-4">
        <div className="space-y-1">
          <p className="px-2 text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Workspace
          </p>
          <span
            aria-current="page"
            className="flex items-center gap-2.5 rounded-lg bg-sidebar-accent px-2.5 py-2 text-sm font-medium text-sidebar-accent-foreground"
          >
            <LayoutDashboard className="size-4" aria-hidden="true" />
            Investigation Workspace
          </span>
        </div>

        <div className="space-y-1">
          <p id={ticketsLabelId} className="px-2 text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Tickets
          </p>
          <form className="space-y-2 px-2 py-2" onSubmit={(event) => {
            event.preventDefault()
            if (!isNavigationLocked) tickets.search(tickets.searchText)
          }}>
            <label className="sr-only" htmlFor={searchId}>Search ticket ID or subject</label>
            <input id={searchId} name="q" type="search" value={tickets.searchText} onChange={(event) => tickets.setSearchText(event.target.value)} placeholder="Search ID or subject" disabled={isNavigationLocked} className="w-full rounded-md border bg-background px-2 py-2 text-sm" />
            <div className="flex gap-2">
              <Button type="submit" size="sm" variant="outline" disabled={isNavigationLocked}>Search</Button>
              <Button type="button" size="sm" variant="ghost" onClick={tickets.reload} disabled={isNavigationLocked || tickets.state.status === 'loading'}>Refresh</Button>
            </div>
          </form>
          {tickets.state.status === 'loading' ? <p role="status" className="px-2 text-sm text-muted-foreground">Loading tickets…</p> : null}
          {tickets.state.status === 'error' ? <div role="alert" className="space-y-2 px-2 text-sm"><p>Tickets could not be loaded.</p><Button variant="outline" size="sm" onClick={tickets.reload} disabled={isNavigationLocked}>Retry</Button></div> : null}
          {tickets.state.status === 'success' && tickets.state.data.items.length === 0 ? <p role="status" className="px-2 text-sm text-muted-foreground">No tickets found.</p> : null}
          <ul aria-labelledby={ticketsLabelId} className="space-y-1">
            {(tickets.state.status === 'success' ? tickets.state.data.items : []).map((ticket) => {
              const isActive = ticket.id === selectedTicketId
              return (
                <li key={ticket.id}>
                  <button
                    type="button"
                    onClick={() => onSelectTicket(ticket.id)}
                    aria-current={isActive ? 'true' : undefined}
                    disabled={isNavigationLocked && !isActive}
                    title={isNavigationLocked && !isActive ? 'Wait until the approved draft is saved' : undefined}
                    className={cn(
                      'group flex min-h-11 w-full items-start gap-2.5 rounded-lg border border-transparent px-2.5 py-2 text-left transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50',
                      isActive ? 'border-primary/25 bg-accent' : 'hover:bg-muted',
                    )}
                  >
                    <Ticket
                      className={cn(
                        'mt-0.5 size-4 shrink-0',
                        isActive ? 'text-primary' : 'text-muted-foreground',
                      )}
                      aria-hidden="true"
                    />
                    <span className="min-w-0">
                      <span className="block font-mono text-xs text-muted-foreground">
                        {ticket.sourceId ?? shortTicketId(ticket.id)}
                      </span>
                      <span
                        className={cn(
                          'block truncate text-sm',
                          isActive ? 'font-medium text-foreground' : 'text-sidebar-foreground',
                        )}
                      >
                        {ticket.subject ?? 'Untitled ticket'}
                      </span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {[ticket.customer?.name, ticket.status].filter(Boolean).join(' · ')}
                      </span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
          {tickets.state.status === 'success' ? <div className="space-y-2 px-2 py-3">
            <p role="status" className="text-xs text-muted-foreground">Page {tickets.query.page} · {tickets.state.data.totalCount} tickets</p>
            <nav aria-label="Ticket pages" className="flex gap-2">
              <Button size="sm" variant="outline" disabled={isNavigationLocked || tickets.query.page <= 1} onClick={() => tickets.setPage(tickets.query.page - 1)}>Previous</Button>
              <Button size="sm" variant="outline" disabled={isNavigationLocked || tickets.query.page * tickets.state.data.pageSize >= tickets.state.data.totalCount} onClick={() => tickets.setPage(tickets.query.page + 1)}>Next</Button>
            </nav>
          </div> : null}
        </div>
      </nav>

      <div className="border-t border-sidebar-border px-5 py-4">
        <p className="flex items-center gap-2 text-xs text-muted-foreground">
          <Database className="size-3.5" aria-hidden="true" />
          Read-only · synthetic demo data
        </p>
      </div>
    </div>
  )
}
