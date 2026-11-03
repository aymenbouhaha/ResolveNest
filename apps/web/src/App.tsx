import { useMemo, useState } from 'react'
import { AppShell } from '@/components/layout/AppShell'
import { TooltipProvider } from '@/components/ui/tooltip'
import { DEFAULT_TICKET_ID, DEMO_TICKETS } from '@/features/investigations/demoTickets'
import { InvestigationWorkspace } from '@/features/investigations/InvestigationWorkspace'
import { NavigationLockContext } from '@/lib/navigationLock'

function App() {
  const [selectedTicketId, setSelectedTicketId] = useState(DEFAULT_TICKET_ID)
  const [isNavigationLocked, setNavigationLocked] = useState(false)
  const selectedScenario = DEMO_TICKETS.find((ticket) => ticket.ticketId === selectedTicketId)
  const navigationLock = useMemo(
    () => ({ isLocked: isNavigationLocked, setLocked: setNavigationLocked }),
    [isNavigationLocked],
  )

  const selectTicket = (ticketId: string) => {
    if (!isNavigationLocked) {
      setSelectedTicketId(ticketId)
    }
  }

  return (
    <TooltipProvider delayDuration={200}>
      <NavigationLockContext.Provider value={navigationLock}>
        <AppShell
          selectedTicketId={selectedTicketId}
          onSelectTicket={selectTicket}
          isNavigationLocked={isNavigationLocked}
        >
          <InvestigationWorkspace
            ticketId={selectedTicketId}
            scenarioLabel={selectedScenario?.label ?? null}
          />
        </AppShell>
      </NavigationLockContext.Provider>
    </TooltipProvider>
  )
}

export default App
