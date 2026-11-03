import axios, { type AxiosError } from 'axios'
import type { ProblemDetails } from '../types/investigation'
import { UnexpectedInvestigationResponseError } from './investigationResponseGuard'

export type InvestigationErrorKind =
  | 'backendUnavailable'
  | 'timeout'
  | 'invalidRequest'
  | 'notFound'
  | 'databaseUnavailable'
  | 'serverError'
  | 'unexpectedResponse'
  | 'unknown'

export interface InvestigationError {
  kind: InvestigationErrorKind
  title: string
  message: string
  retryable: boolean
  httpStatus?: number
  technicalDetail?: string
}

export const TIMEOUT_ERROR_CODES = new Set(['ECONNABORTED', 'ETIMEDOUT'])
const PROXY_FAILURE_STATUSES = new Set([502, 504])

export function isRequestCancelled(error: unknown): boolean {
  return axios.isCancel(error) || (axios.isAxiosError(error) && error.code === 'ERR_CANCELED')
}

export function readProblemDetails(data: unknown): ProblemDetails | null {
  if (typeof data !== 'object' || data === null) {
    return null
  }
  const candidate = data as ProblemDetails
  return typeof candidate.title === 'string' || typeof candidate.status === 'number'
    ? candidate
    : null
}

function describeValidationErrors(problem: ProblemDetails | null): string | undefined {
  if (!problem?.errors) {
    return problem?.detail
  }
  return Object.entries(problem.errors)
    .map(([field, messages]) => `${field}: ${messages.join(' ')}`)
    .join('; ')
}

function fromHttpResponse(error: AxiosError): InvestigationError {
  const httpStatus = error.response?.status ?? 0
  const problem = readProblemDetails(error.response?.data)
  const technicalDetail = [`HTTP ${httpStatus}`, problem?.title, problem?.detail]
    .filter(Boolean)
    .join(' · ')

  if (httpStatus === 400) {
    return {
      kind: 'invalidRequest',
      title: 'This ticket reference is not valid',
      message: 'The investigation could not start because the ticket reference was not recognized.',
      retryable: false,
      httpStatus,
      technicalDetail: describeValidationErrors(problem) ?? technicalDetail,
    }
  }

  if (httpStatus === 404) {
    return {
      kind: 'notFound',
      title: 'Ticket not found',
      message: 'No support ticket with this reference exists. It may have been removed or mistyped.',
      retryable: false,
      httpStatus,
      technicalDetail,
    }
  }

  if (httpStatus === 503) {
    return {
      kind: 'databaseUnavailable',
      title: 'Investigation data is unavailable',
      message:
        'The investigation service is running but cannot reach its data right now. Try again in a moment.',
      retryable: true,
      httpStatus,
      technicalDetail,
    }
  }

  if (PROXY_FAILURE_STATUSES.has(httpStatus) || (httpStatus >= 500 && problem === null)) {
    return {
      kind: 'backendUnavailable',
      title: 'Investigation service unavailable',
      message:
        'ResolveNest could not reach the investigation service. Check that it is running, then try again.',
      retryable: true,
      httpStatus,
      technicalDetail,
    }
  }

  if (httpStatus >= 500) {
    return {
      kind: 'serverError',
      title: 'The investigation could not be completed',
      message:
        'Something went wrong while preparing this investigation. Try again; if it keeps failing, contact the team that runs ResolveNest.',
      retryable: true,
      httpStatus,
      technicalDetail,
    }
  }

  return {
    kind: 'unknown',
    title: 'Unexpected response',
    message: 'The investigation service returned a response this workspace cannot handle. Try again.',
    retryable: true,
    httpStatus,
    technicalDetail,
  }
}

export function toInvestigationError(error: unknown): InvestigationError {
  if (error instanceof UnexpectedInvestigationResponseError) {
    return {
      kind: 'unexpectedResponse',
      title: 'Investigation could not be displayed',
      message:
        'The investigation service returned information in a format this workspace cannot display.',
      retryable: true,
      technicalDetail: error.message,
    }
  }

  if (!axios.isAxiosError(error)) {
    return {
      kind: 'unknown',
      title: 'Something went wrong',
      message: 'The investigation could not be loaded.',
      retryable: true,
      technicalDetail: error instanceof Error ? error.message : undefined,
    }
  }

  if (error.code && TIMEOUT_ERROR_CODES.has(error.code)) {
    return {
      kind: 'timeout',
      title: 'The investigation took too long',
      message: 'The investigation service did not respond in time. Try again in a moment.',
      retryable: true,
      technicalDetail: error.message,
    }
  }

  if (!error.response) {
    return {
      kind: 'backendUnavailable',
      title: 'Investigation service unavailable',
      message:
        'No response was received. Check your connection and that the investigation service is running, then try again.',
      retryable: true,
      technicalDetail: error.message,
    }
  }

  return fromHttpResponse(error)
}
