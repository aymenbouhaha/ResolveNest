import type { EntityNameLookup } from './citationLabels'
import { formatCustomerReference, formatReference } from './recordReferences'

const PREFIXED_IDENTIFIER = /\b(?:rec:)?(ticket|bug|kb|release|customer|feature):([A-Za-z0-9][A-Za-z0-9._-]*[A-Za-z0-9])/g

export function formatIdentifiersForDisplay(text: string, names: EntityNameLookup): string {
  return text.replace(PREFIXED_IDENTIFIER, (match, prefix: string, reference: string) => {
    const canonicalId = `${prefix}:${reference}`
    const knownName = names.get(canonicalId)
    if (knownName) {
      return knownName
    }
    if (prefix === 'customer') {
      return formatCustomerReference(canonicalId)
    }
    return match.startsWith('rec:') ? formatReference(canonicalId) : reference
  })
}
