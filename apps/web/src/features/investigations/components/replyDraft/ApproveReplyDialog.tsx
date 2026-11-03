import { ShieldCheck } from 'lucide-react'
import type { RefObject } from 'react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'

interface ApproveReplyDialogProps {
  textToApprove: string | null
  ticketReference: string
  customerName: string | null
  isEdited: boolean
  onConfirm: (finalText: string) => void
  onCancel: () => void
  returnFocusRef: RefObject<HTMLButtonElement | null>
}

export function ApproveReplyDialog({
  textToApprove,
  ticketReference,
  customerName,
  isEdited,
  onConfirm,
  onCancel,
  returnFocusRef,
}: ApproveReplyDialogProps) {
  return (
    <AlertDialog open={textToApprove !== null} onOpenChange={(open) => (open ? undefined : onCancel())}>
      <AlertDialogContent
        onCloseAutoFocus={(event) => {
          if (returnFocusRef.current && !returnFocusRef.current.disabled) {
            event.preventDefault()
            returnFocusRef.current.focus()
          }
        }}
        className="data-[size=default]:max-w-[calc(100%-2rem)] data-[size=default]:sm:max-w-xl">
        <AlertDialogHeader>
          <AlertDialogTitle>Approve and save this reply?</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-2 text-left text-sm text-muted-foreground">
              <p>
                The text below will be saved as the approved reply draft for{' '}
                <span className="font-medium text-foreground">
                  {ticketReference}
                  {customerName ? ` (${customerName})` : ''}
                </span>
                .
              </p>
              <ul className="list-disc space-y-1 pl-5">
                <li>
                  <span className="font-medium text-foreground">No email is sent.</span> Saving does
                  not contact or notify the customer.
                </li>
                <li>You are responsible for checking that every statement is accurate.</li>
                {isEdited ? (
                  <li>Your edits have not been automatically checked against the investigation evidence.</li>
                ) : null}
              </ul>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        {textToApprove !== null ? (
          <div className="space-y-1">
            <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Text to save</p>
            <div
              tabIndex={0}
              aria-label="Text to save"
              className="max-h-60 overflow-y-auto rounded-lg border bg-muted/30 p-3 text-sm whitespace-pre-wrap outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
            >
              {textToApprove}
            </div>
          </div>
        ) : null}
        <AlertDialogFooter>
          <AlertDialogCancel>Keep reviewing</AlertDialogCancel>
          <AlertDialogAction
            onClick={() => {
              if (textToApprove !== null) {
                onConfirm(textToApprove)
              }
            }}
          >
            <ShieldCheck aria-hidden="true" />
            Approve &amp; save
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
