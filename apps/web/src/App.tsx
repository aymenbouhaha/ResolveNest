import { useMemo, useState } from 'react'
import { AppShell } from '@/components/layout/AppShell'
import { TooltipProvider } from '@/components/ui/tooltip'
import { useTickets } from '@/features/tickets/useTickets'
import { InvestigationWorkspace } from '@/features/investigations/InvestigationWorkspace'
import { NavigationLockContext } from '@/lib/navigationLock'

function App() {
  const [selectedTicket, setSelectedTicket] = useState<{ id: string; subject: string | null } | null>(null)
  const tickets = useTickets()
  const initialTicket = tickets.state.status === 'success' ? tickets.state.data.items[0] : null
  if (selectedTicket === null && initialTicket) setSelectedTicket(initialTicket)
  const selectedTicketId = selectedTicket?.id ?? initialTicket?.id ?? null
  const [isNavigationLocked, setNavigationLocked] = useState(false)
  const navigationLock = useMemo(
    () => ({ isLocked: isNavigationLocked, setLocked: setNavigationLocked }),
    [isNavigationLocked],
  )

  const selectTicket = (ticketId: string) => {
    if (!isNavigationLocked) {
      const ticket = tickets.state.status === 'success' ? tickets.state.data.items.find((item) => item.id === ticketId) : null
      if (ticket) setSelectedTicket(ticket)
    }
  }

  return (
    <TooltipProvider delayDuration={200}>
      <NavigationLockContext.Provider value={navigationLock}>
        <AppShell
          selectedTicketId={selectedTicketId}
          onSelectTicket={selectTicket}
          isNavigationLocked={isNavigationLocked}
          tickets={tickets}
        >
          {selectedTicketId ? <InvestigationWorkspace
            key={selectedTicketId}
            ticketId={selectedTicketId}
            subject={selectedTicket?.subject ?? null}
          /> : <p role="status" className="text-sm text-muted-foreground">{tickets.state.status === 'loading' ? 'Loading tickets…' : tickets.state.status === 'error' ? 'Tickets could not be loaded. Retry in the ticket navigation.' : 'No tickets found. Search again or refresh the ticket list.'}</p>}
        </AppShell>
      </NavigationLockContext.Provider>
    </TooltipProvider>
  )
}

export default App
