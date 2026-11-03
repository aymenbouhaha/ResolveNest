import {
  CircleAlert,
  Footprints,
  Lightbulb,
  Library,
  ShieldAlert,
  Sparkles,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import { useMemo, type ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import {
  buildEntityNameLookup,
  FINDING_KIND_LABELS,
  presentCitation,
  type CitationPresentation,
} from '../../lib/citationLabels'
import { formatIdentifiersForDisplay } from '../../lib/answerTextFormatting'
import { EVIDENCE_STYLES } from '../../lib/evidenceStyles'
import type { TicketInvestigation } from '../../types/investigation'
import type {
  AiFinding,
  AiFindingKind,
  AiStatement,
  InvestigationAnswer,
} from '../../types/investigationAnswer'
import { CitationChips, SupportingRecordsList } from './AnswerCitations'

const UNCONFIRMED_ISSUE_FINDING_KINDS = new Set<AiFindingKind>(['candidate', 'historicalConfirmation'])

interface InvestigationAnswerViewProps {
  question: string
  answer: InvestigationAnswer
  investigation: TicketInvestigation
}

export function InvestigationAnswerView({ question, answer, investigation }: InvestigationAnswerViewProps) {
  const names = useMemo(() => buildEntityNameLookup(investigation), [investigation])
  const citations = useMemo(
    () => answer.validatedSources.map((source) => presentCitation(source, names)),
    [answer.validatedSources, names],
  )
  const formatText = (text: string) => formatIdentifiersForDisplay(text, names)
  const citationsById = useMemo(
    () => new Map(citations.map((citation) => [citation.citationId, citation])),
    [citations],
  )
  const isInconclusive = answer.status === 'insufficientEvidence'
  const mentionsUnconfirmedIssues =
    investigation.confirmedBugs.length === 0 &&
    answer.findings.some((finding) => UNCONFIRMED_ISSUE_FINDING_KINDS.has(finding.kind))

  return (
    <article aria-labelledby="investigation-answer-heading" className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <h3 id="investigation-answer-heading" className="text-sm font-semibold text-foreground">
            Investigation answer
          </h3>
          <Badge variant="outline" className="border-primary/30 bg-accent text-accent-foreground">
            <Sparkles aria-hidden="true" />
            AI-generated
          </Badge>
          {isInconclusive ? (
            <Badge
              variant="outline"
              className="border-evidence-candidate-border bg-evidence-candidate-surface text-evidence-candidate"
            >
              <CircleAlert aria-hidden="true" />
              Not enough evidence
            </Badge>
          ) : null}
        </div>
        <p className="text-xs text-muted-foreground">
          <span className="font-medium">Question:</span> {question}
        </p>
      </header>

      {isInconclusive ? (
        <p className="rounded-lg border border-evidence-candidate-border bg-evidence-candidate-surface px-3 py-2 text-sm text-foreground">
          The available records do not support a definite answer. The points below explain what is
          known and what is missing.
        </p>
      ) : null}

      <AnswerText text={formatText(answer.answer)} />

      {mentionsUnconfirmedIssues ? (
        <p className="flex gap-2 rounded-lg border border-evidence-candidate-border bg-evidence-candidate-surface px-3 py-2 text-sm text-foreground">
          <CircleAlert className="mt-0.5 size-4 shrink-0 text-evidence-candidate" aria-hidden="true" />
          No product issue has been confirmed for this ticket. Issues mentioned in this answer are
          possible matches until a support engineer confirms them.
        </p>
      ) : null}

      {answer.findings.length > 0 ? (
        <AnswerBlock icon={Lightbulb} title="Key findings">
          <ul className="space-y-3">
            {answer.findings.map((finding, index) => (
              <FindingItem
                key={index}
                finding={finding}
                citations={citationsById}
                formatText={formatText}
              />
            ))}
          </ul>
        </AnswerBlock>
      ) : null}

      {answer.uncertainties.length > 0 ? (
        <AnswerBlock icon={TriangleAlert} title="What remains uncertain">
          <StatementList
            statements={answer.uncertainties}
            citations={citationsById}
            formatText={formatText}
          />
        </AnswerBlock>
      ) : null}

      {answer.recommendedNextSteps.length > 0 ? (
        <AnswerBlock
          icon={Footprints}
          title="Suggested next steps"
          caption="Suggestions only. Nothing is done automatically."
        >
          <StatementList
            statements={answer.recommendedNextSteps}
            citations={citationsById}
            formatText={formatText}
            ordered
          />
        </AnswerBlock>
      ) : null}

      <AnswerBlock icon={Library} title="Supporting records">
        <SupportingRecordsList citations={citations} />
      </AnswerBlock>

      <Separator />
      <footer className="space-y-1 text-xs text-muted-foreground">
        {answer.provenance.humanReviewRequired ? (
          <p className="flex items-center gap-1.5">
            <ShieldAlert className="size-3.5 shrink-0" aria-hidden="true" />
            Generated by AI. Review it against the records before relying on it or sharing it.
          </p>
        ) : null}
        {!answer.provenance.evidenceComplete ? (
          <p className="pl-5">
            Some details of this investigation were shortened or left out of what the assistant
            reviewed.
          </p>
        ) : null}
      </footer>
    </article>
  )
}

function AnswerText({ text }: { text: string }) {
  const paragraphs = text
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph.length > 0)

  return (
    <div className="space-y-2 text-sm leading-relaxed text-foreground">
      {paragraphs.map((paragraph, index) => (
        <p key={index} className="whitespace-pre-line">
          {paragraph}
        </p>
      ))}
    </div>
  )
}

interface AnswerBlockProps {
  icon: LucideIcon
  title: string
  caption?: string
  children: ReactNode
}

function AnswerBlock({ icon: Icon, title, caption, children }: AnswerBlockProps) {
  return (
    <section className="space-y-2">
      <div>
        <h4 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
          <Icon className="size-3.5" aria-hidden="true" />
          {title}
        </h4>
        {caption ? <p className="text-xs text-muted-foreground">{caption}</p> : null}
      </div>
      {children}
    </section>
  )
}

interface FindingItemProps {
  finding: AiFinding
  citations: ReadonlyMap<string, CitationPresentation>
  formatText: (text: string) => string
}

function FindingItem({ finding, citations, formatText }: FindingItemProps) {
  const { label, evidence } = FINDING_KIND_LABELS[finding.kind]
  const style = evidence ? EVIDENCE_STYLES[evidence] : null
  const Icon = style?.icon ?? TriangleAlert

  return (
    <li className="rounded-lg border bg-muted/20 p-3">
      <Badge
        variant="outline"
        className={cn(
          'mb-1.5',
          style?.className ?? 'border-destructive/30 bg-destructive/5 text-destructive',
        )}
      >
        <Icon aria-hidden="true" />
        {label}
      </Badge>
      <p className="text-sm">{formatText(finding.statement)}</p>
      <CitationChips citationIds={finding.citationIds} citations={citations} />
    </li>
  )
}

interface StatementListProps {
  statements: AiStatement[]
  citations: ReadonlyMap<string, CitationPresentation>
  formatText: (text: string) => string
  ordered?: boolean
}

function StatementList({ statements, citations, formatText, ordered = false }: StatementListProps) {
  const ListTag = ordered ? 'ol' : 'ul'
  return (
    <ListTag className={cn('space-y-2.5 pl-5 text-sm', ordered ? 'list-decimal' : 'list-disc')}>
      {statements.map((statement, index) => (
        <li key={index} className="pl-1 marker:text-muted-foreground">
          {formatText(statement.statement)}
          <CitationChips citationIds={statement.citationIds} citations={citations} />
        </li>
      ))}
    </ListTag>
  )
}
