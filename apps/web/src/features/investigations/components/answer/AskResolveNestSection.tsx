import { CircleX, Loader2, RefreshCw, Sparkles, X } from 'lucide-react'
import { useId, useState, type FormEvent, type KeyboardEvent } from 'react'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import type { AskError } from '../../api/investigationAskErrors'
import { useInvestigationAsk } from '../../hooks/useInvestigationAsk'
import { MAX_QUESTION_CHARACTERS, validateQuestion } from '../../lib/questionRules'
import type { TicketInvestigation } from '../../types/investigation'
import { InvestigationAnswerView } from './InvestigationAnswerView'

const EXAMPLE_QUESTIONS = [
  'Have other customers experienced this issue, and is there a known fix?',
  'Is this a known product defect or could it be related to configuration?',
  'What should we investigate next?',
]

interface AskResolveNestSectionProps {
  investigation: TicketInvestigation
}

export function AskResolveNestSection({ investigation }: AskResolveNestSectionProps) {
  const ticketId = investigation.ticket.id
  const { state, ask, cancel, clear } = useInvestigationAsk(ticketId)
  const [draftQuestion, setDraftQuestion] = useState('')
  const questionFieldId = useId()
  const questionHintId = useId()
  const validation = validateQuestion(draftQuestion)
  const validationMessage = validation.message
  const isAsking = state.status === 'asking'
  const canSubmit = validation.isValid && !isAsking
  const characterCount = draftQuestion.trim().length

  const submitQuestion = () => {
    if (validation.isValid && !isAsking) {
      void ask(validation.question)
    }
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    submitQuestion()
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      submitQuestion()
    }
  }

  return (
    <section id="ask-resolvenest" aria-labelledby="ask-resolvenest-heading" className="scroll-mt-20">
      <Card className="gap-0 py-0">
        <CardHeader className="gap-1 border-b py-4">
          <CardTitle id="ask-resolvenest-heading" className="flex items-center gap-2 text-base font-semibold">
            <Sparkles className="size-4 text-primary" aria-hidden="true" />
            Ask ResolveNest
          </CardTitle>
          <CardDescription>
            Ask a question about this ticket. The AI assistant answers from this ticket's records and
            cites them; review its answer before relying on it.
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4 py-4">
          <form onSubmit={handleSubmit} className="space-y-3">
            <label htmlFor={questionFieldId} className="text-sm font-medium text-foreground">
              Your question
            </label>
            <Textarea
              id={questionFieldId}
              value={draftQuestion}
              onChange={(event) => setDraftQuestion(event.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="For example: Is there a known fix for this problem?"
              rows={3}
              disabled={isAsking}
              aria-describedby={questionHintId}
              aria-invalid={validationMessage !== null}
              className="max-h-48 bg-card"
            />
            <div className="flex flex-wrap gap-1.5" aria-label="Example questions">
              {EXAMPLE_QUESTIONS.map((example) => (
                <Button
                  key={example}
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-auto max-w-full py-1 text-left whitespace-normal text-muted-foreground"
                  disabled={isAsking}
                  onClick={() => setDraftQuestion(example)}
                >
                  {example}
                </Button>
              ))}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p id={questionHintId} className="text-xs text-muted-foreground" aria-live="polite">
                {validationMessage ? (
                  <span className="text-destructive">{validationMessage}</span>
                ) : (
                  <>
                    {characterCount.toLocaleString('en')} / {MAX_QUESTION_CHARACTERS.toLocaleString('en')}
                    <span className="hidden sm:inline"> · Ctrl+Enter to ask</span>
                  </>
                )}
              </p>
              <div className="flex gap-2">
                {isAsking ? (
                  <Button type="button" variant="ghost" onClick={cancel}>
                    <X aria-hidden="true" />
                    Cancel
                  </Button>
                ) : null}
                <Button type="submit" disabled={!canSubmit}>
                  {isAsking ? (
                    <Loader2 className="animate-spin" aria-hidden="true" />
                  ) : (
                    <Sparkles aria-hidden="true" />
                  )}
                  {isAsking ? 'Investigating' : 'Ask'}
                </Button>
              </div>
            </div>
          </form>

          {state.status === 'asking' ? <AskingPlaceholder question={state.question} /> : null}

          {state.status === 'failed' ? (
            <AskErrorAlert
              error={state.error}
              onRetry={() => void ask(state.question)}
              onDismiss={clear}
            />
          ) : null}

          {state.status === 'answered' ? (
            <div className="space-y-3 rounded-xl border bg-card p-4">
              <InvestigationAnswerView
                question={state.question}
                answer={state.answer}
                investigation={investigation}
              />
              <div className="flex justify-end">
                <Button type="button" variant="ghost" size="sm" onClick={clear}>
                  <CircleX aria-hidden="true" />
                  Clear answer
                </Button>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </section>
  )
}

function AskingPlaceholder({ question }: { question: string }) {
  return (
    <div role="status" aria-live="polite" className="space-y-3 rounded-xl border bg-muted/20 p-4">
      <p className="flex items-center gap-2 text-sm font-medium">
        <Loader2 className="size-4 animate-spin text-primary" aria-hidden="true" />
        Reviewing this ticket's records…
      </p>
      <p className="text-xs text-muted-foreground">
        <span className="font-medium">Question:</span> {question}
      </p>
      <div className="space-y-2" aria-hidden="true">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-11/12" />
        <Skeleton className="h-4 w-3/5" />
      </div>
    </div>
  )
}

interface AskErrorAlertProps {
  error: AskError
  onRetry: () => void
  onDismiss: () => void
}

function AskErrorAlert({ error, onRetry, onDismiss }: AskErrorAlertProps) {
  return (
    <Alert variant="destructive" className="gap-1.5 px-4 py-4">
      <CircleX aria-hidden="true" />
      <AlertTitle>{error.title}</AlertTitle>
      <AlertDescription className="space-y-3">
        <p className="text-foreground/80">{error.message}</p>
        <div className="flex flex-wrap gap-2">
          {error.retryable ? (
            <Button type="button" variant="outline" size="sm" onClick={onRetry}>
              <RefreshCw aria-hidden="true" />
              Please try again
            </Button>
          ) : null}
          <Button type="button" variant="ghost" size="sm" onClick={onDismiss}>
            Dismiss
          </Button>
        </div>
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
