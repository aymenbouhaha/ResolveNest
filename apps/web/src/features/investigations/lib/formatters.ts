const dateFormatter = new Intl.DateTimeFormat('en', { dateStyle: 'medium' })
const dateTimeFormatter = new Intl.DateTimeFormat('en', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

function parseDate(value: string | null): Date | null {
  if (!value) {
    return null
  }
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

export function formatDate(value: string | null): string | null {
  const date = parseDate(value)
  return date ? dateFormatter.format(date) : null
}

export function formatDateTime(value: string | null): string | null {
  const date = parseDate(value)
  return date ? dateTimeFormatter.format(date) : null
}

export function humanizeToken(value: string | null): string | null {
  if (!value) {
    return null
  }
  const spaced = value.replace(/[_-]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase()
}

export function pluralize(count: number, singular: string, plural = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : plural}`
}
