import type { components } from './schema';

type Schemas = components['schemas'];

export type UserDto = Schemas['UserDto'];
export type AuthResponse = Schemas['AuthResponse'];
export type LoginRequest = Schemas['LoginRequest'];
export type RegisterRequest = Schemas['RegisterRequest'];

export type ConversationSummary = Schemas['ConversationSummaryDto'];
export type Conversation = Schemas['ConversationDto'];
export type Message = Schemas['MessageDto'];
export type MessageRole = Schemas['MessageRole'];
export type Citation = Schemas['Citation'];
export type TokenUsage = Schemas['TokenUsage'];
export type SendMessageRequest = Schemas['SendMessageRequest'];

export type DocumentItem = Schemas['DocumentDto'];
export type DocumentStatus = Schemas['DocumentStatus'];
export type DocumentPage = Schemas['PagedResultOfDocumentDto'];

export const DOCUMENT_STATUSES: readonly DocumentStatus[] = [
  'Pending',
  'Processing',
  'Ready',
  'Failed',
];

export interface ProblemDetails {
  readonly type?: string | null;
  readonly title?: string | null;
  readonly status?: number | null;
  readonly detail?: string | null;
  readonly instance?: string | null;
  readonly code?: string | null;
  readonly traceId?: string | null;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}
