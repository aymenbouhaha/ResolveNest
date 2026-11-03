import { CircleX, RefreshCw } from 'lucide-react'
import type { ReactNode } from 'react'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'

interface ServiceErrorAlertProps {
  title: string
  message: string
  technicalDetail?: string
  retryLabel?: string
  onRetry?: () => void
  onDismiss?: () => void
  dismissLabel?: string
  children?: ReactNode
}

export function ServiceErrorAlert({
  title,
  message,
  technicalDetail,
  retryLabel = 'Please try again',
  onRetry,
  onDismiss,
  dismissLabel = 'Dismiss',
  children,
}: ServiceErrorAlertProps) {
  return (
    <Alert variant="destructive" className="gap-1.5 px-4 py-4">
      <CircleX aria-hidden="true" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription className="space-y-3">
        <p className="text-foreground/80">{message}</p>
        {children}
        {onRetry || onDismiss ? (
          <div className="flex flex-wrap gap-2">
            {onRetry ? (
              <Button type="button" variant="outline" size="sm" onClick={onRetry}>
                <RefreshCw aria-hidden="true" />
                {retryLabel}
              </Button>
            ) : null}
            {onDismiss ? (
              <Button type="button" variant="ghost" size="sm" onClick={onDismiss}>
                {dismissLabel}
              </Button>
            ) : null}
          </div>
        ) : null}
        {technicalDetail ? (
          <Accordion type="single" collapsible>
            <AccordionItem value="technical-details">
              <AccordionTrigger className="py-1.5 text-xs text-muted-foreground">
                Technical details
              </AccordionTrigger>
              <AccordionContent>
                <p className="font-mono text-xs break-all text-muted-foreground">{technicalDetail}</p>
              </AccordionContent>
            </AccordionItem>
          </Accordion>
        ) : null}
      </AlertDescription>
    </Alert>
  )
}
