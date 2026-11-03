import { FlaskConical, Menu } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { shortTicketId } from '@/features/investigations/demoTickets'

interface AppHeaderProps {
  selectedTicketId: string
  onOpenNavigation: () => void
}

export function AppHeader({ selectedTicketId, onOpenNavigation }: AppHeaderProps) {
  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-background/90 px-4 backdrop-blur sm:px-6">
      <Button
        variant="ghost"
        size="icon-lg"
        className="lg:hidden"
        onClick={onOpenNavigation}
        aria-label="Open navigation"
      >
        <Menu aria-hidden="true" />
      </Button>
      <div className="min-w-0 flex-1">
        <nav aria-label="Breadcrumb">
          <ol className="flex min-w-0 items-center gap-1.5 text-sm">
            <li className="hidden text-muted-foreground sm:block">Investigation Workspace</li>
            <li className="hidden text-muted-foreground sm:block" aria-hidden="true">
              /
            </li>
            <li aria-current="page" className="truncate font-mono font-medium text-foreground">
              {shortTicketId(selectedTicketId)}
            </li>
          </ol>
        </nav>
      </div>
      <Badge variant="outline" className="hidden gap-1 text-muted-foreground sm:inline-flex">
        <FlaskConical aria-hidden="true" />
        Synthetic demo data
      </Badge>
    </header>
  )
}
