import { CircleAlert, Info, PenLine, RotateCcw, Sparkles } from 'lucide-react'
import { useId } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { MAX_FINAL_TEXT_CHARACTERS, type TextValidation } from '../../lib/replyDraftRules'
import type { ReplyDraftStatus } from '../../types/replyDraft'

const EDITED_TEXT_WARNING =
  'Your edits have not been automatically checked against the investigation evidence. Review the final text before saving.'

interface ReplyDraftEditorProps {
  draftText: string
  draftStatus: ReplyDraftStatus
  isEdited: boolean
  isReadOnly: boolean
  validation: TextValidation
  onChange: (text: string) => void
  onRestoreGenerated: () => void
}

export function ReplyDraftEditor({
  draftText,
  draftStatus,
  isEdited,
  isReadOnly,
  validation,
  onChange,
  onRestoreGenerated,
}: ReplyDraftEditorProps) {
  const fieldId = useId()
  const hintId = useId()
  const characterCount = draftText.trim().length

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <label htmlFor={fieldId} className="text-sm font-semibold text-foreground">
          Proposed reply to the customer
        </label>
        <Badge variant="outline" className="border-primary/30 bg-accent text-accent-foreground">
          <Sparkles aria-hidden="true" />
          AI-generated · needs your review
        </Badge>
        {isEdited ? (
          <Badge variant="secondary">
            <PenLine aria-hidden="true" />
            Edited by you
          </Badge>
        ) : null}
        {draftStatus === 'insufficientEvidence' ? (
          <Badge
            variant="outline"
            className="border-evidence-candidate-border bg-evidence-candidate-surface text-evidence-candidate"
          >
            <CircleAlert aria-hidden="true" />
            Holding reply
          </Badge>
        ) : null}
      </div>

      {draftStatus === 'insufficientEvidence' ? (
        <p className="text-sm text-muted-foreground">
          The records do not support a definite answer, so this draft acknowledges the problem and asks
          for more information instead of naming a cause.
        </p>
      ) : null}

      <Textarea
        id={fieldId}
        value={draftText}
        onChange={(event) => onChange(event.target.value)}
        readOnly={isReadOnly}
        rows={14}
        aria-describedby={hintId}
        aria-invalid={!validation.isValid}
        className="min-h-72 bg-card leading-relaxed"
      />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p id={hintId} className="text-xs text-muted-foreground" aria-live="polite">
          {validation.message ? (
            <span className="text-destructive">{validation.message}</span>
          ) : (
            `${characterCount.toLocaleString('en')} / ${MAX_FINAL_TEXT_CHARACTERS.toLocaleString('en')} characters`
          )}
        </p>
        {isEdited && !isReadOnly ? (
          <Button type="button" variant="ghost" size="sm" onClick={onRestoreGenerated}>
            <RotateCcw aria-hidden="true" />
            Restore generated text
          </Button>
        ) : null}
      </div>

      {isEdited ? (
        <p className="flex gap-2 rounded-lg border border-evidence-candidate-border bg-evidence-candidate-surface px-3 py-2 text-sm text-foreground">
          <CircleAlert className="mt-0.5 size-4 shrink-0 text-evidence-candidate" aria-hidden="true" />
          {EDITED_TEXT_WARNING}
        </p>
      ) : (
        <p className="flex gap-2 text-xs text-muted-foreground">
          <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
          This text passed ResolveNest's automated checks when it was generated. Those checks catch
          some unsupported statements but do not prove the reply is correct; verify every claim against
          the records before approving.
        </p>
      )}
    </div>
  )
}
