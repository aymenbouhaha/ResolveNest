import axios from 'axios'
import { toAskError, type AskErrorKind } from './investigationAskErrors'
import { readProblemDetails, TIMEOUT_ERROR_CODES } from './investigationErrors'
import { UnexpectedReplyDraftResponseError } from './replyDraftGuards'

export interface ReplyDraftError {
  title: string
  message: string
  retryable: boolean
  technicalDetail?: string
}

export type ApprovalErrorKind =
  | 'outcomeUnknown'
  | 'previewExpired'
  | 'previewInvalid'
  | 'conflict'
  | 'invalidText'
  | 'ticketNotFound'
  | 'notSavedDatabaseUnavailable'
  | 'storageNotReady'

export interface ApprovalError extends ReplyDraftError {
  kind: ApprovalErrorKind
}

const GENERIC_ASK_TITLE = 'Unable to complete the investigation'

function technicalDetailFor(error: unknown): string | undefined {
  if (!axios.isAxiosError(error)) {
    return error instanceof Error ? error.message : undefined
  }
  const problem = readProblemDetails(error.response?.data)
  const validation = problem?.errors
    ? Object.entries(problem.errors).map(([field, messages]) => `${field}: ${messages.join(' ')}`)
    : []
  return [
    error.response ? `HTTP ${error.response.status}` : error.code,
    problem?.reason,
    problem?.title,
    ...validation,
    ...(problem?.validationFailures ?? []),
  ]
    .filter(Boolean)
    .join(' · ')
}

export function toPreviewError(error: unknown): ReplyDraftError {
  if (error instanceof UnexpectedReplyDraftResponseError) {
    return {
      title: 'Unable to display the preview',
      message: 'The preview was returned in a format this workspace cannot display. Please try again.',
      retryable: true,
      technicalDetail: error.message,
    }
  }

  if (axios.isAxiosError(error) && error.response) {
    const problem = readProblemDetails(error.response.data)
    if (problem?.reason === 'draftRejected') {
      return {
        title: 'The draft did not pass evidence checks',
        message:
          'ResolveNest discarded the generated reply because it was not fully supported by the ticket records. Please try again or adjust your instructions.',
        retryable: true,
        technicalDetail: technicalDetailFor(error),
      }
    }
    if (error.response.status === 400) {
      return {
        title: 'The instructions could not be used',
        message: 'Keep reviewer instructions to 500 characters or fewer, without special control characters.',
        retryable: false,
        technicalDetail: technicalDetailFor(error),
      }
    }
  }

  const askError = toAskError(error)
  const previewWording = PREVIEW_WORDING[askError.kind]
  return {
    title: previewWording?.title ?? (askError.title === GENERIC_ASK_TITLE ? 'Unable to generate a preview' : askError.title),
    message: previewWording?.message ?? askError.message,
    retryable: askError.retryable,
    technicalDetail: askError.technicalDetail,
  }
}

const PREVIEW_WORDING: Partial<Record<AskErrorKind, { title: string; message: string }>> = {
  assistantTimeout: {
    title: 'Preview generation took too long',
    message: 'No draft was produced in time. Nothing was saved. Please try again.',
  },
  ticketNotFound: {
    title: 'Ticket not found',
    message: 'This ticket is no longer available, so no draft could be prepared.',
  },
  dataUnavailable: {
    title: 'Unable to generate a preview',
    message: 'The ticket information needed for a draft is unavailable right now. Please try again in a moment.',
  },
  answerBlocked: {
    title: 'No draft could be produced',
    message: 'The AI assistant declined to draft this reply. Try different instructions.',
  },
  serverError: {
    title: 'Unable to generate a preview',
    message: 'Something went wrong while preparing the draft. Please try again.',
  },
  unexpectedResponse: {
    title: 'Unable to display the preview',
    message: 'The preview was returned in a format this workspace cannot display. Please try again.',
  },
}

const APPROVAL_ERRORS: Record<ApprovalErrorKind, Omit<ApprovalError, 'technicalDetail'>> = {
  outcomeUnknown: {
    kind: 'outcomeUnknown',
    title: 'We could not confirm whether the draft was saved',
    message:
      'The approval was sent, but no clear answer came back. It may or may not have been saved. Re-sending the same approval is safe: if it was already saved, ResolveNest will show the saved draft instead of creating a second one.',
    retryable: true,
  },
  previewExpired: {
    kind: 'previewExpired',
    title: 'This preview has expired',
    message:
      'Previews can be approved for 30 minutes. Nothing was saved. Generate a new preview, review it again, then approve it.',
    retryable: false,
  },
  previewInvalid: {
    kind: 'previewInvalid',
    title: 'This preview can no longer be approved',
    message: 'The preview was not accepted for this ticket. Nothing was saved. Generate a new preview and review it again.',
    retryable: false,
  },
  conflict: {
    kind: 'conflict',
    title: 'This preview was already approved with different text',
    message:
      'An approved draft already exists for this preview, so your text was not saved. Generate a new preview if you need to approve a different reply.',
    retryable: false,
  },
  invalidText: {
    kind: 'invalidText',
    title: 'The reply could not be saved',
    message:
      'The final text did not meet the requirements for an approved draft. Nothing was saved. Check the text and try again.',
    retryable: false,
  },
  ticketNotFound: {
    kind: 'ticketNotFound',
    title: 'Ticket not found',
    message: 'This ticket is no longer available. Nothing was saved.',
    retryable: false,
  },
  notSavedDatabaseUnavailable: {
    kind: 'notSavedDatabaseUnavailable',
    title: 'The draft was not saved',
    message: 'The investigation data store is unavailable right now. Nothing was saved. Please try again in a moment.',
    retryable: true,
  },
  storageNotReady: {
    kind: 'storageNotReady',
    title: 'Approved drafts cannot be saved yet',
    message: 'Saving approved drafts is not set up in this environment. Nothing was saved.',
    retryable: false,
  },
}

function approvalError(kind: ApprovalErrorKind, technicalDetail?: string): ApprovalError {
  return { ...APPROVAL_ERRORS[kind], technicalDetail }
}

function approvalKindFromResponse(httpStatus: number, reason: string | undefined, hasProblem: boolean): ApprovalErrorKind {
  switch (reason) {
    case 'previewTokenExpired':
      return 'previewExpired'
    case 'invalidPreviewToken':
    case 'previewTicketMismatch':
      return 'previewInvalid'
    case 'replyDraftConflict':
      return 'conflict'
    case 'replyDraftStorageNotInitialized':
      return 'storageNotReady'
  }

  if (httpStatus === 400) {
    return 'invalidText'
  }
  if (httpStatus === 404) {
    return 'ticketNotFound'
  }
  if (httpStatus === 503 && hasProblem) {
    return 'notSavedDatabaseUnavailable'
  }
  return 'outcomeUnknown'
}

export function toApprovalError(error: unknown): ApprovalError {
  if (error instanceof UnexpectedReplyDraftResponseError) {
    return approvalError('outcomeUnknown', error.message)
  }

  if (!axios.isAxiosError(error) || !error.response || (error.code && TIMEOUT_ERROR_CODES.has(error.code))) {
    return approvalError('outcomeUnknown', technicalDetailFor(error))
  }

  const problem = readProblemDetails(error.response.data)
  return approvalError(
    approvalKindFromResponse(error.response.status, problem?.reason, problem !== null),
    technicalDetailFor(error),
  )
}
