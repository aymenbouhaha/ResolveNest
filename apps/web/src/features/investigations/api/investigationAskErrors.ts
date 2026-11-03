import axios, { type AxiosError } from 'axios'
import type { ProblemDetails } from '../types/investigation'
import { readProblemDetails, TIMEOUT_ERROR_CODES } from './investigationErrors'
import { UnexpectedInvestigationAnswerError } from './investigationAnswerGuard'

export type AskErrorKind =
  | 'invalidQuestion'
  | 'ticketNotFound'
  | 'dataUnavailable'
  | 'assistantNotConfigured'
  | 'rateLimited'
  | 'assistantTimeout'
  | 'answerBlocked'
  | 'answerRejected'
  | 'assistantUnavailable'
  | 'serviceUnavailable'
  | 'serverError'
  | 'unexpectedResponse'

export interface AskError {
  kind: AskErrorKind
  title: string
  message: string
  retryable: boolean
  technicalDetail?: string
}

const ASSISTANT_UNAVAILABLE_MESSAGE =
  'The AI investigation is temporarily unavailable. Your existing ticket information is still accessible.'

const ASK_ERRORS: Record<AskErrorKind, Omit<AskError, 'technicalDetail'>> = {
  invalidQuestion: {
    kind: 'invalidQuestion',
    title: 'The question could not be sent',
    message: 'Check that the question is between 5 and 1,000 characters, then try again.',
    retryable: false,
  },
  ticketNotFound: {
    kind: 'ticketNotFound',
    title: 'Ticket not found',
    message: 'This ticket is no longer available, so the question could not be answered.',
    retryable: false,
  },
  dataUnavailable: {
    kind: 'dataUnavailable',
    title: 'Unable to complete the investigation',
    message: 'The ticket information needed to answer is unavailable right now. Please try again in a moment.',
    retryable: true,
  },
  assistantNotConfigured: {
    kind: 'assistantNotConfigured',
    title: 'AI investigation is not available',
    message: 'The AI assistant is not set up for this environment. Your existing ticket information is still accessible.',
    retryable: false,
  },
  rateLimited: {
    kind: 'rateLimited',
    title: 'AI request limit reached',
    message:
      'The AI assistant cannot take more requests right now because a usage limit was reached. Please try again later.',
    retryable: true,
  },
  assistantTimeout: {
    kind: 'assistantTimeout',
    title: 'The AI investigation took too long',
    message: 'No answer was produced in time. Please try again.',
    retryable: true,
  },
  answerBlocked: {
    kind: 'answerBlocked',
    title: 'No answer could be produced',
    message: 'The AI assistant declined to answer this question. Try rephrasing it.',
    retryable: false,
  },
  answerRejected: {
    kind: 'answerRejected',
    title: 'The answer did not pass evidence checks',
    message:
      'ResolveNest discarded the generated answer because it was not fully supported by the ticket records. Please try again or rephrase the question.',
    retryable: true,
  },
  assistantUnavailable: {
    kind: 'assistantUnavailable',
    title: 'Unable to complete the investigation',
    message: ASSISTANT_UNAVAILABLE_MESSAGE,
    retryable: true,
  },
  serviceUnavailable: {
    kind: 'serviceUnavailable',
    title: 'Unable to complete the investigation',
    message: 'ResolveNest could not reach the investigation service. Please try again.',
    retryable: true,
  },
  serverError: {
    kind: 'serverError',
    title: 'Unable to complete the investigation',
    message: 'Something went wrong while preparing the answer. Please try again.',
    retryable: true,
  },
  unexpectedResponse: {
    kind: 'unexpectedResponse',
    title: 'Unable to display the answer',
    message: 'The answer was returned in a format this workspace cannot display. Please try again.',
    retryable: true,
  },
}

function askError(kind: AskErrorKind, technicalDetail?: string): AskError {
  return { ...ASK_ERRORS[kind], technicalDetail }
}

function describeProblem(httpStatus: number, problem: ProblemDetails | null): string {
  const validation = problem?.errors
    ? Object.entries(problem.errors).map(([field, messages]) => `${field}: ${messages.join(' ')}`)
    : []
  return [
    `HTTP ${httpStatus}`,
    problem?.reason,
    problem?.title,
    ...validation,
    ...(problem?.validationFailures ?? []),
  ]
    .filter(Boolean)
    .join(' · ')
}

function kindFromProblem(httpStatus: number, problem: ProblemDetails | null): AskErrorKind {
  if (problem === null) {
    return httpStatus >= 500 ? 'serviceUnavailable' : 'serverError'
  }

  switch (problem.reason) {
    case 'aiNotConfigured':
    case 'aiAuthenticationFailed':
    case 'aiModelUnavailable':
      return 'assistantNotConfigured'
    case 'aiRateLimited':
      return 'rateLimited'
    case 'aiTimeout':
      return 'assistantTimeout'
    case 'aiResponseBlocked':
      return 'answerBlocked'
    case 'answerRejected':
      return 'answerRejected'
    case 'evidenceTooLarge':
      return 'serverError'
  }

  if (problem.reason?.startsWith('ai')) {
    return 'assistantUnavailable'
  }

  switch (httpStatus) {
    case 400:
      return 'invalidQuestion'
    case 404:
      return 'ticketNotFound'
    case 429:
      return 'rateLimited'
    case 503:
      return 'dataUnavailable'
    case 504:
      return 'assistantTimeout'
    default:
      return 'serverError'
  }
}

function fromHttpResponse(error: AxiosError): AskError {
  const httpStatus = error.response?.status ?? 0
  const problem = readProblemDetails(error.response?.data)
  return askError(kindFromProblem(httpStatus, problem), describeProblem(httpStatus, problem))
}

export function toAskError(error: unknown): AskError {
  if (error instanceof UnexpectedInvestigationAnswerError) {
    return askError('unexpectedResponse', error.message)
  }

  if (!axios.isAxiosError(error)) {
    return askError('serverError', error instanceof Error ? error.message : undefined)
  }

  if (error.code && TIMEOUT_ERROR_CODES.has(error.code)) {
    return askError('assistantTimeout', error.message)
  }

  if (!error.response) {
    return askError('serviceUnavailable', error.message)
  }

  return fromHttpResponse(error)
}
