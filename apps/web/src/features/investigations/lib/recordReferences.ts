const SOURCE_SYSTEM_LABELS: Record<string, string> = {
  'synthetic-helpdesk': 'Help desk',
  'synthetic-crm': 'CRM',
  'synthetic-issue-tracker': 'Issue tracker',
  'synthetic-kb': 'Knowledge base',
  'synthetic-product-catalog': 'Product catalog',
}

export function formatReference(id: string): string {
  const separatorIndex = id.indexOf(':')
  return separatorIndex === -1 ? id : id.slice(separatorIndex + 1)
}

export function formatCustomerReference(customerId: string | null): string {
  if (!customerId) {
    return 'Unknown customer'
  }
  return formatReference(customerId)
    .split('-')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ')
}

export function formatSourceSystem(sourceSystem: string | null): string | null {
  if (!sourceSystem) {
    return null
  }
  return SOURCE_SYSTEM_LABELS[sourceSystem] ?? null
}

export function describeSourceRecord(
  sourceSystem: string | null,
  sourceId: string | null,
): string | null {
  const systemLabel = formatSourceSystem(sourceSystem)
  if (systemLabel && sourceId) {
    return `${systemLabel} · ${sourceId}`
  }
  return systemLabel ?? sourceId
}

export function joinWithAnd(items: string[]): string {
  if (items.length <= 1) {
    return items.join('')
  }
  return `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`
}
