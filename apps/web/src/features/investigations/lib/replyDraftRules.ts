export const MAX_REVIEWER_GUIDANCE_CHARACTERS = 500
export const MIN_FINAL_TEXT_CHARACTERS = 40
export const MAX_FINAL_TEXT_CHARACTERS = 4_000

const ALLOWED_WHITESPACE_CODE_POINTS = new Set([9, 10, 13])
const INTERNAL_ARTIFACT = /\b(rec|rel):\S+|\{\{|\}\}|\[(insert|customer|name|todo)[^\]]*\]|<insert/i

function hasDisallowedControlCharacter(text: string): boolean {
  return [...text].some((character) => {
    const codePoint = character.codePointAt(0) ?? 0
    const isControl = codePoint < 32 || (codePoint >= 127 && codePoint <= 159)
    return isControl && !ALLOWED_WHITESPACE_CODE_POINTS.has(codePoint)
  })
}

export interface TextValidation {
  isValid: boolean
  message: string | null
}

export function validateReviewerGuidance(rawGuidance: string): TextValidation {
  const guidance = rawGuidance.trim()
  if (guidance.length > MAX_REVIEWER_GUIDANCE_CHARACTERS) {
    return {
      isValid: false,
      message: `Keep instructions to ${MAX_REVIEWER_GUIDANCE_CHARACTERS} characters or fewer.`,
    }
  }
  if (hasDisallowedControlCharacter(guidance)) {
    return { isValid: false, message: 'Remove unsupported special characters from the instructions.' }
  }
  return { isValid: true, message: null }
}

export function validateFinalText(rawText: string): TextValidation {
  const text = rawText.trim()
  if (text.length === 0) {
    return { isValid: false, message: 'The reply cannot be empty.' }
  }
  if (text.length < MIN_FINAL_TEXT_CHARACTERS) {
    return {
      isValid: false,
      message: `The reply must be at least ${MIN_FINAL_TEXT_CHARACTERS} characters.`,
    }
  }
  if (text.length > MAX_FINAL_TEXT_CHARACTERS) {
    return {
      isValid: false,
      message: `Keep the reply to ${MAX_FINAL_TEXT_CHARACTERS.toLocaleString('en')} characters or fewer.`,
    }
  }
  if (hasDisallowedControlCharacter(text)) {
    return { isValid: false, message: 'Remove unsupported special characters from the reply.' }
  }
  if (INTERNAL_ARTIFACT.test(text)) {
    return {
      isValid: false,
      message: 'Remove internal record references or unfilled placeholders such as [Customer Name] or {{ }}.',
    }
  }
  return { isValid: true, message: null }
}

export function normalizeForComparison(text: string): string {
  return text.replace(/\r\n/g, '\n').trim()
}
