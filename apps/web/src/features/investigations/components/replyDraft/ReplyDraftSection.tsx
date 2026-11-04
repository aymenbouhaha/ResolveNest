import { FileText, Loader2, MailX, RefreshCw, ShieldCheck, Trash2, X } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
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
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'
import { useNavigationLock } from '@/lib/navigationLock'
import { EDITABLE_AFTER_FAILURE, useReplyDraft, type ReplyDraftState } from '../../hooks/useReplyDraft'
import { formatReference } from '../../lib/recordReferences'
import {
  MAX_REVIEWER_GUIDANCE_CHARACTERS,
  normalizeForComparison,
  validateFinalText,
  validateReviewerGuidance,
} from '../../lib/replyDraftRules'
import type { TicketInvestigation } from '../../types/investigation'
import { ServiceErrorAlert } from '../ServiceErrorAlert'
import { ApprovedDraftSummary } from './ApprovedDraftSummary'
import { ApproveReplyDialog } from './ApproveReplyDialog'
import { ReplyDraftEditor } from './ReplyDraftEditor'
import { ReplyDraftEvidencePanel } from './ReplyDraftEvidencePanel'

const EXPIRY_CHECK_INTERVAL_MS = 15_000

const WORKFLOW_STEPS = ['Generate preview', 'Review and edit', 'Approve & save'] as const

function currentStepIndex(state: ReplyDraftState): number {
  switch (state.phase) {
    case 'idle':
    case 'generating':
    case 'generationFailed':
      return 0
    case 'approved':
      return 3
    default:
      return 1
  }
}

function useCurrentTime(): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), EXPIRY_CHECK_INTERVAL_MS)
    return () => window.clearInterval(timer)
  }, [])
  return now
}

interface ReplyDraftSectionProps {
  investigation: TicketInvestigation
}

export function ReplyDraftSection({ investigation }: ReplyDraftSectionProps) {
  const ticketId = investigation.ticket.id
  const ticketReference = formatReference(ticketId)
  const { state, generatePreview, cancelPreview, updateDraftText, approve, resendApproval, startOver } =
    useReplyDraft(ticketId)
  const { setLocked } = useNavigationLock()
  const [guidance, setGuidance] = useState('')
  const [textAwaitingConfirmation, setTextAwaitingConfirmation] = useState<string | null>(null)
  const [isDiscardDialogOpen, setDiscardDialogOpen] = useState(false)
  const approveButtonRef = useRef<HTMLButtonElement>(null)
  const newPreviewButtonRef = useRef<HTMLButtonElement>(null)
  const guidanceFieldId = useId()
  const guidanceHintId = useId()
  const now = useCurrentTime()

  const isApproving = state.phase === 'approving'

  useEffect(() => {
    setLocked(isApproving)
  }, [isApproving, setLocked])

  useEffect(() => () => setLocked(false), [setLocked])

  useEffect(() => {
    if (!isApproving) {
      return
    }
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warnBeforeLeaving)
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving)
  }, [isApproving])

  const guidanceValidation = validateReviewerGuidance(guidance)
  const hasDraft = state.phase === 'reviewing' || state.phase === 'approving' || state.phase === 'approvalFailed'
  const draftText = hasDraft ? state.draftText : ''
  const generatedText = hasDraft ? state.preview.draftBody : ''
  const isEdited = hasDraft && normalizeForComparison(draftText) !== normalizeForComparison(generatedText)
  const isExpired = hasDraft && Date.parse(state.preview.previewExpiresAt) <= now
  const finalTextValidation = validateFinalText(draftText)
  const isEditable =
    state.phase === 'reviewing' ||
    (state.phase === 'approvalFailed' && EDITABLE_AFTER_FAILURE.has(state.error.kind))
  const canApprove = isEditable && finalTextValidation.isValid && !isExpired

  const requestNewPreview = () => {
    if (isEdited) {
      setDiscardDialogOpen(true)
      return
    }
    void generatePreview(guidance.trim() || null)
  }

  return (
    <section id="customer-reply-draft" aria-labelledby="customer-reply-draft-heading" className="scroll-mt-20">
      <Card className="gap-0 py-0">
        <CardHeader className="gap-1 border-b py-4">
          <h2 id="customer-reply-draft-heading" data-slot="card-title" className="flex items-center gap-2 font-heading leading-snug text-base font-semibold">
            <FileText className="size-4 text-primary" aria-hidden="true" />
            Customer reply draft
          </h2>
          <CardDescription>
            Generate a suggested reply, review and edit it, then approve it to save it as the approved
            draft for this ticket. ResolveNest never sends email.
          </CardDescription>
          <WorkflowSteps activeIndex={currentStepIndex(state)} />
        </CardHeader>

        <CardContent className="space-y-5 py-4">
          {state.phase === 'idle' || state.phase === 'generating' || state.phase === 'generationFailed' ? (
            <form
              className="space-y-3"
              onSubmit={(event) => {
                event.preventDefault()
                if (guidanceValidation.isValid && state.phase !== 'generating') {
                  void generatePreview(guidance.trim() || null)
                }
              }}
            >
              <label htmlFor={guidanceFieldId} className="text-sm font-medium text-foreground">
                Instructions for the draft <span className="font-normal text-muted-foreground">(optional)</span>
              </label>
              <Textarea
                id={guidanceFieldId}
                value={guidance}
                onChange={(event) => setGuidance(event.target.value)}
                placeholder="For example: Keep the reply concise and ask the customer to confirm their installed patch version."
                rows={2}
                disabled={state.phase === 'generating'}
                aria-describedby={guidanceHintId}
                aria-invalid={!guidanceValidation.isValid}
                className="max-h-40 bg-card"
              />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p id={guidanceHintId} className="text-xs text-muted-foreground" aria-live="polite">
                  {guidanceValidation.message ? (
                    <span className="text-destructive">{guidanceValidation.message}</span>
                  ) : (
                    `Instructions can shape tone and focus; they cannot add facts. ${guidance.trim().length} / ${MAX_REVIEWER_GUIDANCE_CHARACTERS}`
                  )}
                </p>
                <div className="flex gap-2">
                  {state.phase === 'generating' ? (
                    <Button type="button" variant="ghost" onClick={cancelPreview}>
                      <X aria-hidden="true" />
                      Cancel
                    </Button>
                  ) : null}
                  <Button type="submit" disabled={!guidanceValidation.isValid || state.phase === 'generating'}>
                    {state.phase === 'generating' ? (
                      <Loader2 className="animate-spin" aria-hidden="true" />
                    ) : (
                      <FileText aria-hidden="true" />
                    )}
                    {state.phase === 'generating' ? 'Generating' : 'Generate preview'}
                  </Button>
                </div>
              </div>
            </form>
          ) : null}

          {state.phase === 'generating' ? <GeneratingPlaceholder /> : null}

          {state.phase === 'generationFailed' ? (
            <ServiceErrorAlert
              title={state.error.title}
              message={state.error.message}
              technicalDetail={state.error.technicalDetail}
              onRetry={state.error.retryable ? () => void generatePreview(guidance.trim() || null) : undefined}
              onDismiss={startOver}
            />
          ) : null}

          {hasDraft ? (
            <div className="space-y-5">
              <ReplyDraftEditor
                draftText={draftText}
                draftStatus={state.preview.status}
                isEdited={isEdited}
                isReadOnly={!isEditable}
                validation={finalTextValidation}
                onChange={updateDraftText}
                onRestoreGenerated={() => updateDraftText(generatedText)}
              />

              {isExpired && state.phase !== 'approving' ? (
                <ServiceErrorAlert
                  title="This preview has expired"
                  message="Previews can be approved for 30 minutes. Nothing was saved. Generate a new preview and review it again."
                />
              ) : null}

              {state.phase === 'approving' ? <ApprovingNotice /> : null}

              {state.phase === 'approvalFailed' ? (
                <ServiceErrorAlert
                  title={state.error.title}
                  message={state.error.message}
                  technicalDetail={state.error.technicalDetail}
                  retryLabel={state.error.kind === 'outcomeUnknown' ? 'Re-send the same approval' : 'Try saving again'}
                  onRetry={state.error.retryable ? resendApproval : undefined}
                  onDismiss={EDITABLE_AFTER_FAILURE.has(state.error.kind) ? undefined : startOver}
                  dismissLabel="Start over with a new preview"
                />
              ) : null}

              <div className="flex flex-wrap items-center gap-2">
                <Button
                  ref={approveButtonRef}
                  type="button"
                  disabled={!canApprove}
                  onClick={() => setTextAwaitingConfirmation(draftText.trim())}
                >
                  {isApproving ? <Loader2 className="animate-spin" aria-hidden="true" /> : <ShieldCheck aria-hidden="true" />}
                  {isApproving ? 'Saving' : 'Approve & save'}
                </Button>
                <Button
                  ref={newPreviewButtonRef}
                  type="button"
                  variant="outline"
                  disabled={isApproving}
                  onClick={requestNewPreview}
                >
                  <RefreshCw aria-hidden="true" />
                  Generate new preview
                </Button>
                <Button type="button" variant="ghost" disabled={isApproving} onClick={startOver}>
                  <Trash2 aria-hidden="true" />
                  Discard draft
                </Button>
                <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <MailX className="size-3.5" aria-hidden="true" />
                  Approving saves the draft. It does not send an email.
                </p>
              </div>

              <ReplyDraftEvidencePanel preview={state.preview} investigation={investigation} />
            </div>
          ) : null}

          {state.phase === 'approved' ? (
            <ApprovedDraftSummary result={state.result} onStartOver={startOver} />
          ) : null}
        </CardContent>
      </Card>

      <ApproveReplyDialog
        textToApprove={textAwaitingConfirmation}
        ticketReference={ticketReference}
        customerName={investigation.customer?.name ?? null}
        isEdited={isEdited}
        onCancel={() => setTextAwaitingConfirmation(null)}
        returnFocusRef={approveButtonRef}
        onConfirm={(finalText) => {
          setTextAwaitingConfirmation(null)
          approve(finalText)
        }}
      />

      <AlertDialog open={isDiscardDialogOpen} onOpenChange={setDiscardDialogOpen}>
        <AlertDialogContent
          onCloseAutoFocus={(event) => {
            if (newPreviewButtonRef.current?.isConnected && !newPreviewButtonRef.current.disabled) {
              event.preventDefault()
              newPreviewButtonRef.current.focus()
            }
          }}
        >
          <AlertDialogHeader>
            <AlertDialogTitle>Discard your edits?</AlertDialogTitle>
            <AlertDialogDescription>
              Generating a new preview replaces the current draft, including your edits.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep my edits</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => void generatePreview(guidance.trim() || null)}
            >
              Discard and regenerate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  )
}

function WorkflowSteps({ activeIndex }: { activeIndex: number }) {
  return (
    <ol className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs" aria-label="Reply workflow">
      {WORKFLOW_STEPS.map((step, index) => {
        const isDone = index < activeIndex
        const isActive = index === activeIndex
        return (
          <li
            key={step}
            aria-current={isActive ? 'step' : undefined}
            className={cn(
              'flex items-center gap-1.5',
              isActive ? 'font-medium text-foreground' : 'text-muted-foreground',
            )}
          >
            <span
              className={cn(
                'flex size-5 items-center justify-center rounded-full border text-[0.65rem] tabular-nums',
                isActive && 'border-primary bg-primary text-primary-foreground',
                isDone && 'border-evidence-confirmed-border bg-evidence-confirmed-surface text-evidence-confirmed',
              )}
            >
              {index + 1}
            </span>
            {step}
          </li>
        )
      })}
    </ol>
  )
}

function GeneratingPlaceholder() {
  return (
    <div role="status" aria-live="polite" className="space-y-3 rounded-xl border bg-muted/20 p-4">
      <p className="flex items-center gap-2 text-sm font-medium">
        <Loader2 className="size-4 animate-spin text-primary" aria-hidden="true" />
        Preparing a suggested reply from this ticket's records…
      </p>
      <div className="space-y-2" aria-hidden="true">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-11/12" />
        <Skeleton className="h-4 w-4/5" />
        <Skeleton className="h-4 w-2/3" />
      </div>
    </div>
  )
}

function ApprovingNotice() {
  return (
    <p role="status" aria-live="polite" className="flex items-center gap-2 rounded-lg border bg-muted/30 px-3 py-2 text-sm">
      <Loader2 className="size-4 animate-spin text-primary" aria-hidden="true" />
      Saving the approved draft. Ticket navigation is paused until the server responds.
    </p>
  )
}
