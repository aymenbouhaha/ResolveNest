import type { EvidenceClassification, TicketInvestigation } from '../types/investigation'
import type { AiFindingKind, CitationSource } from '../types/investigationAnswer'
import { formatCustomerReference, formatReference } from './recordReferences'

export type EntityNameLookup = ReadonlyMap<string, string>

export function buildEntityNameLookup({ customer, features }: TicketInvestigation): EntityNameLookup {
  const names = new Map<string, string>()
  if (customer?.name) {
    names.set(customer.id, customer.name)
  }
  for (const feature of features) {
    if (feature.name) {
      names.set(feature.id, feature.name)
    }
  }
  return names
}

function entityName(entityId: string, names: EntityNameLookup): string {
  const knownName = names.get(entityId)
  if (knownName) {
    return knownName
  }
  if (entityId.startsWith('release:')) {
    return `version ${formatReference(entityId)}`
  }
  if (entityId.startsWith('customer:')) {
    return formatCustomerReference(entityId)
  }
  return formatReference(entityId)
}

const RECORD_TYPE_LABELS: Record<string, string> = {
  SupportTicket: 'Support case',
  Bug: 'Product issue',
  KnowledgeArticle: 'Troubleshooting guide',
  Release: 'Release',
  Customer: 'Customer',
  Feature: 'Product feature',
}

type ConnectionDescriber = (from: string, to: string) => string

const CONNECTION_DESCRIBERS: Record<string, ConnectionDescriber> = {
  REPORTED: (from, to) => `${from} reported ${to}`,
  ABOUT_FEATURE: (from, to) => `${from} concerns ${to}`,
  CONFIRMED_AS: (from, to) => `${from} confirmed as ${to}`,
  AFFECTS: (from, to) => `${from} affects ${to}`,
  INTRODUCED_IN: (from, to) => `${from} introduced in ${to}`,
  FIXED_IN: (from, to) => `${from} fixed in ${to}`,
  ADDRESSES: (from, to) => `${from} addresses ${to}`,
  COVERS: (from, to) => `${from} covers ${to}`,
}

export interface CitationPresentation {
  citationId: string
  label: string
  evidence: EvidenceClassification | null
  isConnection: boolean
}

export function presentCitation(source: CitationSource, names: EntityNameLookup): CitationPresentation {
  if (source.kind === 'relationship' && source.relationship) {
    const { type, fromId, toId, evidence } = source.relationship
    const from = entityName(fromId, names)
    const to = entityName(toId, names)
    const describe = CONNECTION_DESCRIBERS[type] ?? ((left: string, right: string) => `${left} and ${right}`)
    return { citationId: source.citationId, label: describe(from, to), evidence, isConnection: true }
  }

  const typeLabel = RECORD_TYPE_LABELS[source.entityType]
  const name =
    source.entityType === 'Release' ? formatReference(source.entityId) : entityName(source.entityId, names)
  return {
    citationId: source.citationId,
    label: typeLabel ? `${typeLabel} ${name}` : name,
    evidence: null,
    isConnection: false,
  }
}

export const FINDING_KIND_LABELS: Record<
  AiFindingKind,
  { label: string; evidence: EvidenceClassification | null }
> = {
  confirmedForTicket: { label: 'Confirmed for this ticket', evidence: 'confirmed' },
  historicalConfirmation: { label: 'Confirmed on other cases', evidence: 'documented' },
  candidate: { label: 'Possible', evidence: 'candidate' },
  documented: { label: 'Documented', evidence: 'documented' },
  context: { label: 'Background', evidence: 'context' },
  contradiction: { label: 'Conflicting evidence', evidence: null },
}
