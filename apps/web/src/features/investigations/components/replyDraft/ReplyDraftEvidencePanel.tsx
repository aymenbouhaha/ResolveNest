import { ClipboardList, Library, ListChecks, ShieldAlert, TriangleAlert, type LucideIcon } from 'lucide-react'
import { useMemo, type ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { formatIdentifiersForDisplay } from '../../lib/answerTextFormatting'
import { buildEntityNameLookup, presentCitation } from '../../lib/citationLabels'
import { describeEvidenceLimitation } from '../../lib/evidenceLimitationText'
import { formatDateTime } from '../../lib/formatters'
import type { TicketInvestigation } from '../../types/investigation'
import type { AiStatement } from '../../types/investigationAnswer'
import type { ReplyDraftPreview } from '../../types/replyDraft'
import { CitationChips, SupportingRecordsList } from '../answer/AnswerCitations'

interface ReplyDraftEvidencePanelProps {
  preview: ReplyDraftPreview
  investigation: TicketInvestigation
}

export function ReplyDraftEvidencePanel({ preview, investigation }: ReplyDraftEvidencePanelProps) {
  const names = useMemo(() => buildEntityNameLookup(investigation), [investigation])
  const citations = useMemo(
    () => preview.validatedSources.map((source) => presentCitation(source, names)),
    [preview.validatedSources, names],
  )
  const citationsById = useMemo(
    () => new Map(citations.map((citation) => [citation.citationId, citation])),
    [citations],
  )
  const formatText = (text: string) => formatIdentifiersForDisplay(text, names)
  const expiresAt = formatDateTime(preview.previewExpiresAt)

  return (
    <section
      aria-labelledby="reply-evidence-heading"
      className="space-y-4 rounded-xl border border-dashed bg-muted/20 p-4"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="reply-evidence-heading" className="text-sm font-semibold text-foreground">
          Information used to prepare this draft
        </h3>
        <Badge variant="outline" className="text-muted-foreground">
          Internal · not part of the reply
        </Badge>
      </div>

      {preview.supportingClaims.length > 0 ? (
        <PanelBlock icon={ListChecks} title="Statements the draft relies on">
          <StatementList statements={preview.supportingClaims} formatText={formatText}>
            {(statement) => <CitationChips citationIds={statement.citationIds} citations={citationsById} />}
          </StatementList>
        </PanelBlock>
      ) : null}

      {preview.uncertainties.length > 0 ? (
        <PanelBlock icon={TriangleAlert} title="What remains uncertain">
          <StatementList statements={preview.uncertainties} formatText={formatText}>
            {(statement) => <CitationChips citationIds={statement.citationIds} citations={citationsById} />}
          </StatementList>
        </PanelBlock>
      ) : null}

      {preview.evidenceLimitations.length > 0 ? (
        <PanelBlock icon={ClipboardList} title="Investigation limitations">
          <ul className="list-disc space-y-1.5 pl-5 text-sm marker:text-muted-foreground">
            {preview.evidenceLimitations.map((limitation) => (
              <li key={limitation.code} className="pl-1">
                {describeEvidenceLimitation(limitation, investigation, names)}
              </li>
            ))}
          </ul>
        </PanelBlock>
      ) : null}

      <PanelBlock icon={Library} title="Supporting records">
        <SupportingRecordsList citations={citations} subject="draft" />
      </PanelBlock>

      <PanelBlock icon={ShieldAlert} title="Review details">
        <dl className="grid gap-x-4 gap-y-1 text-xs text-muted-foreground sm:grid-cols-[auto_1fr]">
          <dt className="font-medium">Human review</dt>
          <dd>{preview.humanReviewRequired ? 'Required before approval' : 'Not flagged'}</dd>
          <dt className="font-medium">Saved</dt>
          <dd>{preview.persisted ? 'Yes' : 'No. Previews are never saved until you approve.'}</dd>
          {expiresAt ? (
            <>
              <dt className="font-medium">Approvable until</dt>
              <dd>{expiresAt}</dd>
            </>
          ) : null}
          {!preview.provenance.evidenceComplete ? (
            <>
              <dt className="font-medium">Evidence reviewed</dt>
              <dd>Some details of this investigation were shortened or left out.</dd>
            </>
          ) : null}
        </dl>
      </PanelBlock>
    </section>
  )
}

interface PanelBlockProps {
  icon: LucideIcon
  title: string
  children: ReactNode
}

function PanelBlock({ icon: Icon, title, children }: PanelBlockProps) {
  return (
    <div className="space-y-2">
      <h4 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
        <Icon className="size-3.5" aria-hidden="true" />
        {title}
      </h4>
      {children}
    </div>
  )
}

interface StatementListProps {
  statements: AiStatement[]
  formatText: (text: string) => string
  children: (statement: AiStatement) => ReactNode
}

function StatementList({ statements, formatText, children }: StatementListProps) {
  return (
    <ul className="list-disc space-y-2 pl-5 text-sm marker:text-muted-foreground">
      {statements.map((statement, index) => (
        <li key={index} className="pl-1">
          {formatText(statement.statement)}
          {children(statement)}
        </li>
      ))}
    </ul>
  )
}
