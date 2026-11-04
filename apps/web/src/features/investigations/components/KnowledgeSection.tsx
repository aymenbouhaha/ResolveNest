import { BookOpen, CornerDownRight } from 'lucide-react'
import { lazy, Suspense } from 'react'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { formatDate, pluralize } from '../lib/formatters'
import { describeKnowledgeRoute } from '../lib/knowledgeRouteText'
import { formatReference } from '../lib/recordReferences'
import type {
  KnowledgeArticleEvidence,
  KnowledgeRoute,
  TicketFeature,
} from '../types/investigation'
import { EmptyEvidence, EvidenceSection } from './EvidenceSection'
import { EvidenceBadge } from './EvidenceBadge'

const GuideMarkdown = lazy(() =>
  import('./GuideMarkdown').then((module) => ({ default: module.GuideMarkdown })),
)

interface KnowledgeSectionProps {
  articles: KnowledgeArticleEvidence[]
  features: TicketFeature[]
}

export function KnowledgeSection({ articles, features }: KnowledgeSectionProps) {
  return (
    <EvidenceSection
      id="troubleshooting-guides"
      icon={BookOpen}
      title="Troubleshooting guides"
      description="Knowledge base articles about this ticket's product area or about the issues above. A guide written for a potential issue applies only if that issue is confirmed."
      meta={articles.length > 0 ? pluralize(articles.length, 'guide') : null}
    >
      {articles.length > 0 ? (
        <div className="grid gap-3">
          {articles.map((article) => (
            <KnowledgeArticleCard key={article.id} article={article} features={features} />
          ))}
        </div>
      ) : (
        <EmptyEvidence>No related troubleshooting guides were found.</EmptyEvidence>
      )}
    </EvidenceSection>
  )
}

interface KnowledgeArticleCardProps {
  article: KnowledgeArticleEvidence
  features: TicketFeature[]
}

function KnowledgeArticleCard({ article, features }: KnowledgeArticleCardProps) {
  const updatedOn = formatDate(article.updatedAt)

  return (
    <Card size="sm" className="min-w-0">
      <CardHeader className="gap-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline" className="font-mono">
            {formatReference(article.id)}
          </Badge>
          {updatedOn ? <span className="text-xs text-muted-foreground">Updated {updatedOn}</span> : null}
        </div>
        <h3 className="text-sm font-semibold">{article.title ?? 'Untitled guide'}</h3>
        {article.summary ? <p className="text-sm text-muted-foreground">{article.summary}</p> : null}
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="space-y-1.5">
          <h4 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Why it is listed</h4>
          <ul className="space-y-2">
            {article.reachedVia.map((route) => (
              <KnowledgeRouteItem
                key={`${route.relationship}-${route.targetId}`}
                route={route}
                features={features}
              />
            ))}
          </ul>
        </div>
        {article.content ? (
          <Accordion type="single" collapsible className="rounded-lg border px-3">
            <AccordionItem value="content">
              <AccordionTrigger>Read full guide</AccordionTrigger>
              <AccordionContent>
                <div
                  tabIndex={0}
                  aria-label={`${formatReference(article.id)} guide content`}
                  className="max-h-96 overflow-y-auto rounded-md bg-muted/40 px-3 py-1 outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
                >
                  <Suspense
                    fallback={
                      <p role="status" className="py-2 text-xs text-muted-foreground">
                        Loading guide…
                      </p>
                    }
                  >
                    <GuideMarkdown content={article.content} />
                  </Suspense>
                </div>
              </AccordionContent>
            </AccordionItem>
          </Accordion>
        ) : null}
      </CardContent>
    </Card>
  )
}

interface KnowledgeRouteItemProps {
  route: KnowledgeRoute
  features: TicketFeature[]
}

function KnowledgeRouteItem({ route, features }: KnowledgeRouteItemProps) {
  return (
    <li className="flex gap-2 text-sm">
      <CornerDownRight className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
      <span className="min-w-0 space-y-1">
        <span className="block">{describeKnowledgeRoute(route, features)}</span>
        <EvidenceBadge evidence={route.evidence} />
      </span>
    </li>
  )
}
