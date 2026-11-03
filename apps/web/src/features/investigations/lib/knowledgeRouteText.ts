import type { KnowledgeRoute, TicketFeature } from '../types/investigation'
import { formatReference } from './recordReferences'

export function describeKnowledgeRoute(route: KnowledgeRoute, features: TicketFeature[]): string {
  const target = formatReference(route.targetId)

  if (route.relationship === 'COVERS') {
    const featureName = features.find((feature) => feature.id === route.targetId)?.name ?? target
    return `Covers ${featureName}, the feature this ticket concerns.`
  }

  if (route.relationship === 'ADDRESSES' && route.evidence === 'confirmed') {
    return `Addresses ${target}, a confirmed issue for this ticket.`
  }

  if (route.relationship === 'ADDRESSES' && route.evidence === 'candidate') {
    return `Addresses ${target}, a potential related issue. Its applicability to this ticket has not been confirmed.`
  }

  return `Related to ${target}.`
}
