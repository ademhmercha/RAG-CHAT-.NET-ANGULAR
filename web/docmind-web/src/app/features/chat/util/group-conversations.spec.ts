import type { ConversationSummary } from '../../../core/api/api-types';
import { groupConversations } from './group-conversations';

const NOW = new Date(2030, 5, 15, 14, 0, 0).getTime();

function conversation(id: string, daysAgo: number, hour = 9): ConversationSummary {
  const date = new Date(2030, 5, 15 - daysAgo, hour, 0, 0).toISOString();
  return { id, title: id, createdAt: date, updatedAt: date } as ConversationSummary;
}

describe('groupConversations', () => {
  it('buckets conversations by recency and keeps newest first', () => {
    const groups = groupConversations(
      [
        conversation('old', 90),
        conversation('today-early', 0, 8),
        conversation('yesterday', 1),
        conversation('week', 5),
        conversation('month', 20),
        conversation('today-late', 0, 13),
      ],
      NOW,
    );

    expect(groups.map((group) => group.key)).toEqual([
      'today',
      'yesterday',
      'week',
      'month',
      'older',
    ]);
    expect(groups[0]?.items.map((item) => item.id)).toEqual(['today-late', 'today-early']);
  });

  it('omits empty groups', () => {
    const groups = groupConversations([conversation('a', 2)], NOW);

    expect(groups.map((group) => group.key)).toEqual(['week']);
  });

  it('returns nothing for an empty list', () => {
    expect(groupConversations([], NOW)).toEqual([]);
  });
});
