import { CircleCheck, Copy, FilePlus2, MailX } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '../../lib/formatters'
import { formatReference } from '../../lib/recordReferences'
import type { ReplyDraftApprovalResult } from '../../types/replyDraft'

interface ApprovedDraftSummaryProps {
  result: ReplyDraftApprovalResult
  onStartOver: () => void
}

const DELIVERY_LABELS: Record<string, string> = {
  notSent: 'Not sent. No email or message was sent to the customer.',
}

export function ApprovedDraftSummary({ result, onStartOver }: ApprovedDraftSummaryProps) {
  const { draft, alreadyApproved } = result
  const [copyStatus, setCopyStatus] = useState<'idle' | 'copied' | 'failed'>('idle')

  const copyApprovedText = async () => {
    try {
      await navigator.clipboard.writeText(draft.approvedText)
      setCopyStatus('copied')
    } catch {
      setCopyStatus('failed')
    }
  }

  return (
    <div className="space-y-4">
      <div
        role="status"
        className="space-y-1.5 rounded-xl border border-evidence-confirmed-border bg-evidence-confirmed-surface p-4"
      >
        <p className="flex items-center gap-2 text-sm font-semibold text-evidence-confirmed">
          <CircleCheck className="size-4" aria-hidden="true" />
          Approved draft saved
        </p>
        <p className="flex items-start gap-2 text-sm text-foreground">
          <MailX className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          No email has been sent. To reply to the customer, copy the approved text into your help desk.
        </p>
        {alreadyApproved ? (
          <p className="text-xs text-muted-foreground">
            This preview had already been approved with the same text, so the existing saved draft is
            shown and nothing new was created.
          </p>
        ) : null}
      </div>

      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
        <SummaryItem label="Ticket">{formatReference(draft.ticketId)}</SummaryItem>
        <SummaryItem label="Approved on">{formatDateTime(draft.approvedAt) ?? draft.approvedAt}</SummaryItem>
        <SummaryItem label="Edited by reviewer">{draft.editedByHuman ? 'Yes' : 'No'}</SummaryItem>
        <SummaryItem label="Automated checks">
          {draft.approvedTextValidated
            ? 'Saved text is the generated text that passed automated checks'
            : 'Saved text includes edits that were not automatically checked'}
        </SummaryItem>
        <SummaryItem label="Customer delivery">
          {DELIVERY_LABELS[draft.customerDelivery] ?? draft.customerDelivery}
        </SummaryItem>
        <SummaryItem label="Draft reference">
          <span className="font-mono text-xs break-all">{draft.draftId}</span>
        </SummaryItem>
      </dl>

      <div className="space-y-1">
        <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Approved text</p>
        <div className="rounded-lg border bg-card p-3 text-sm whitespace-pre-wrap">{draft.approvedText}</div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="button" variant="outline" onClick={() => void copyApprovedText()}>
          <Copy aria-hidden="true" />
          Copy approved text
        </Button>
        <Button type="button" variant="ghost" onClick={onStartOver}>
          <FilePlus2 aria-hidden="true" />
          Start a new draft
        </Button>
        <span className="text-xs text-muted-foreground" aria-live="polite">
          {copyStatus === 'copied' ? 'Copied to clipboard.' : null}
          {copyStatus === 'failed' ? 'Copy failed. Select the text and copy it manually.' : null}
        </span>
      </div>
    </div>
  )
}

function SummaryItem({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0 space-y-0.5">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}
