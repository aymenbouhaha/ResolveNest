import { RefreshCw, ServerCrash } from 'lucide-react'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import type { InvestigationError } from '../api/investigationErrors'
import { formatReference } from '../lib/recordReferences'

export function InvestigationSkeleton() {
  return (
    <div role="status" aria-live="polite" className="space-y-6">
      <span className="sr-only">Loading investigation</span>
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-6">
          <Card>
            <CardHeader className="gap-3">
              <div className="flex gap-2">
                <Skeleton className="h-5 w-24" />
                <Skeleton className="h-5 w-28" />
              </div>
              <Skeleton className="h-7 w-3/4" />
            </CardHeader>
            <CardContent className="space-y-2">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-11/12" />
              <Skeleton className="h-4 w-2/3" />
            </CardContent>
          </Card>
          {[0, 1].map((index) => (
            <Card key={index}>
              <CardHeader className="gap-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-2/3" />
              </CardHeader>
              <CardContent className="grid gap-3 sm:grid-cols-3">
                <Skeleton className="h-20" />
                <Skeleton className="h-20" />
                <Skeleton className="h-20" />
              </CardContent>
            </Card>
          ))}
        </div>
        <div className="hidden space-y-4 xl:block">
          <Skeleton className="h-44 rounded-xl" />
          <Skeleton className="h-56 rounded-xl" />
        </div>
      </div>
    </div>
  )
}

interface InvestigationErrorStateProps {
  ticketId: string
  error: InvestigationError
  onRetry: () => void
}

export function InvestigationErrorState({ ticketId, error, onRetry }: InvestigationErrorStateProps) {
  return (
    <Alert variant="destructive" className="max-w-3xl gap-1.5 px-4 py-4">
      <ServerCrash aria-hidden="true" />
      <AlertTitle className="text-base">{error.title}</AlertTitle>
      <AlertDescription className="space-y-3">
        <p className="text-foreground/80">
          {error.message} <span className="text-muted-foreground">Ticket {formatReference(ticketId)}.</span>
        </p>
        {error.retryable ? (
          <Button variant="outline" onClick={onRetry}>
            <RefreshCw aria-hidden="true" />
            Try again
          </Button>
        ) : null}
        {error.technicalDetail ? (
          <Accordion type="single" collapsible>
            <AccordionItem value="technical-details">
              <AccordionTrigger className="py-1.5 text-xs text-muted-foreground">
                Technical details
              </AccordionTrigger>
              <AccordionContent>
                <p className="font-mono text-xs break-all text-muted-foreground">
                  {error.technicalDetail}
                </p>
              </AccordionContent>
            </AccordionItem>
          </Accordion>
        ) : null}
      </AlertDescription>
    </Alert>
  )
}
