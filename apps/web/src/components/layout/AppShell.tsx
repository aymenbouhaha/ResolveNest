import { useState, type ReactNode } from 'react'
import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { AppHeader } from './AppHeader'
import { AppSidebar } from './AppSidebar'
import type { TicketList } from '@/features/tickets/useTickets'

interface AppShellProps {
  selectedTicketId: string | null
  tickets: TicketList
  onSelectTicket: (ticketId: string) => void
  isNavigationLocked: boolean
  children: ReactNode
}

export function AppShell({
  selectedTicketId,
  onSelectTicket,
  isNavigationLocked,
  children,
  tickets,
}: AppShellProps) {
  const [isNavigationOpen, setNavigationOpen] = useState(false)

  const selectTicketAndCloseNavigation = (ticketId: string) => {
    onSelectTicket(ticketId)
    setNavigationOpen(false)
  }

  return (
    <div className="min-h-svh bg-background">
      <a
        href="#main-content"
        className="sr-only z-50 rounded-md bg-card px-3 py-2 text-sm shadow focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Skip to investigation
      </a>

      <aside className="fixed inset-y-0 left-0 hidden w-72 border-r border-sidebar-border lg:block">
        <AppSidebar
          tickets={tickets}
          selectedTicketId={selectedTicketId}
          onSelectTicket={onSelectTicket}
          isNavigationLocked={isNavigationLocked}
        />
      </aside>

      <Sheet open={isNavigationOpen} onOpenChange={setNavigationOpen}>
        <SheetContent side="left" className="w-72 gap-0 p-0">
          <SheetTitle className="sr-only">Navigation</SheetTitle>
          <SheetDescription className="sr-only">Choose a ticket to investigate</SheetDescription>
          <AppSidebar
            tickets={tickets}
            selectedTicketId={selectedTicketId}
            onSelectTicket={selectTicketAndCloseNavigation}
            isNavigationLocked={isNavigationLocked}
          />
        </SheetContent>
      </Sheet>

      <div className="lg:pl-72">
        <AppHeader
          selectedTicketId={selectedTicketId}
          onOpenNavigation={() => setNavigationOpen(true)}
        />
        <main id="main-content" className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          {children}
        </main>
      </div>
    </div>
  )
}
