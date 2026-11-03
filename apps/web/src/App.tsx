import { useState } from 'react'
import { AppShell } from '@/components/layout/AppShell'
import { TooltipProvider } from '@/components/ui/tooltip'
import { DEFAULT_TICKET_ID, DEMO_TICKETS } from '@/features/investigations/demoTickets'
import { InvestigationWorkspace } from '@/features/investigations/InvestigationWorkspace'

function App() {
  const [selectedTicketId, setSelectedTicketId] = useState(DEFAULT_TICKET_ID)
  const selectedScenario = DEMO_TICKETS.find((ticket) => ticket.ticketId === selectedTicketId)

  return (
    <TooltipProvider delayDuration={200}>
      <AppShell selectedTicketId={selectedTicketId} onSelectTicket={setSelectedTicketId}>
        <InvestigationWorkspace
          ticketId={selectedTicketId}
          scenarioLabel={selectedScenario?.label ?? null}
        />
      </AppShell>
    </TooltipProvider>
  )
}

export default App
