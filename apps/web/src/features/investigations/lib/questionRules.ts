export const MIN_QUESTION_CHARACTERS = 5
export const MAX_QUESTION_CHARACTERS = 1_000

const ALLOWED_WHITESPACE_CODE_POINTS = new Set([9, 10, 13])

function isDisallowedControlCharacter(character: string): boolean {
  const codePoint = character.codePointAt(0) ?? 0
  const isControl = codePoint < 32 || (codePoint >= 127 && codePoint <= 159)
  return isControl && !ALLOWED_WHITESPACE_CODE_POINTS.has(codePoint)
}

export interface QuestionValidation {
  isValid: boolean
  question: string
  message: string | null
}

function invalidQuestion(question: string, message: string | null): QuestionValidation {
  return { isValid: false, question, message }
}

export function validateQuestion(rawQuestion: string): QuestionValidation {
  const question = rawQuestion.trim()

  if (question.length === 0) {
    return invalidQuestion(question, null)
  }

  if (question.length < MIN_QUESTION_CHARACTERS) {
    return invalidQuestion(question, `Enter at least ${MIN_QUESTION_CHARACTERS} characters.`)
  }

  if (question.length > MAX_QUESTION_CHARACTERS) {
    return invalidQuestion(
      question,
      `Keep the question to ${MAX_QUESTION_CHARACTERS.toLocaleString('en')} characters or fewer.`,
    )
  }

  if ([...question].some(isDisallowedControlCharacter)) {
    return invalidQuestion(question, 'Remove unsupported special characters from the question.')
  }

  return { isValid: true, question, message: null }
}
