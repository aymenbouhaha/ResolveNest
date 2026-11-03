import { Database, LayoutDashboard, Ticket, Waypoints } from 'lucide-react'
import { cn } from '@/lib/utils'
import { DEMO_TICKETS, shortTicketId } from '@/features/investigations/demoTickets'

interface AppSidebarProps {
  selectedTicketId: string
  onSelectTicket: (ticketId: string) => void
  isNavigationLocked: boolean
}

export function AppSidebar({ selectedTicketId, onSelectTicket, isNavigationLocked }: AppSidebarProps) {
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
          <p id="demo-tickets-label" className="px-2 text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Demo investigations
          </p>
          <ul aria-labelledby="demo-tickets-label" className="space-y-1">
            {DEMO_TICKETS.map((demoTicket) => {
              const isActive = demoTicket.ticketId === selectedTicketId
              return (
                <li key={demoTicket.ticketId}>
                  <button
                    type="button"
                    onClick={() => onSelectTicket(demoTicket.ticketId)}
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
                        {shortTicketId(demoTicket.ticketId)}
                      </span>
                      <span
                        className={cn(
                          'block truncate text-sm',
                          isActive ? 'font-medium text-foreground' : 'text-sidebar-foreground',
                        )}
                      >
                        {demoTicket.label}
                      </span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {demoTicket.scenario}
                      </span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
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
