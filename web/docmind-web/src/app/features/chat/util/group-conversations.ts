import type { ConversationSummary } from '../../../core/api/api-types';

export type ConversationGroupKey = 'today' | 'yesterday' | 'week' | 'month' | 'older';

export interface ConversationGroup {
  readonly key: ConversationGroupKey;
  readonly items: readonly ConversationSummary[];
}

const GROUP_ORDER: readonly ConversationGroupKey[] = [
  'today',
  'yesterday',
  'week',
  'month',
  'older',
];

const DAY_MS = 86_400_000;

function startOfDay(timestamp: number): number {
  const date = new Date(timestamp);
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

export function groupKeyFor(updatedAt: string, now: number): ConversationGroupKey {
  const today = startOfDay(now);
  const updated = Date.parse(updatedAt);

  if (updated >= today) {
    return 'today';
  }
  if (updated >= today - DAY_MS) {
    return 'yesterday';
  }
  if (updated >= today - 7 * DAY_MS) {
    return 'week';
  }

  return updated >= today - 30 * DAY_MS ? 'month' : 'older';
}

export function groupConversations(
  conversations: readonly ConversationSummary[],
  now: number,
): ConversationGroup[] {
  const buckets = new Map<ConversationGroupKey, ConversationSummary[]>();

  const sorted = [...conversations].sort(
    (a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt),
  );
  for (const conversation of sorted) {
    const key = groupKeyFor(conversation.updatedAt, now);
    buckets.set(key, [...(buckets.get(key) ?? []), conversation]);
  }

  return GROUP_ORDER.flatMap((key) => {
    const items = buckets.get(key);
    return items ? [{ key, items }] : [];
  });
}
