import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { Observable, of, throwError } from 'rxjs';
import type { ConversationSummary } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { provideApiTesting } from '../../../../testing/api-testing';
import { ConversationsApi } from '../data/conversations.api';
import { CONVERSATIONS_NOW, ConversationsStore } from './conversations.store';

const NOW = new Date(2030, 5, 15, 12, 0, 0).getTime();

function summary(id: string, title: string, daysAgo = 0): ConversationSummary {
  const date = new Date(NOW - daysAgo * 86_400_000).toISOString();
  return { id, title, createdAt: date, updatedAt: date } as ConversationSummary;
}

describe('ConversationsStore', () => {
  const api = {
    list: vi.fn(),
    create: vi.fn(),
    rename: vi.fn(),
    remove: vi.fn(),
    get: vi.fn(),
  };
  let store: InstanceType<typeof ConversationsStore>;

  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        { provide: ConversationsApi, useValue: api },
        { provide: CONVERSATIONS_NOW, useValue: () => NOW },
      ],
    });
    store = TestBed.inject(ConversationsStore);
  });

  it('loads conversations newest first and groups them by date', async () => {
    api.list.mockReturnValue(
      of([summary('old', 'Old', 40), summary('new', 'New', 0), summary('mid', 'Mid', 3)]),
    );

    await store.load();

    expect(store.items().map((item) => item.id)).toEqual(['new', 'mid', 'old']);
    expect(store.groups().map((group) => group.key)).toEqual(['today', 'week', 'older']);
    expect(store.loaded()).toBe(true);
  });

  it('filters by title, ignoring case', async () => {
    api.list.mockReturnValue(of([summary('1', 'Quarterly report'), summary('2', 'Holiday plan')]));
    await store.load();

    store.setQuery('  REPORT ');

    expect(store.filtered().map((item) => item.id)).toEqual(['1']);
  });

  it('flags a failed load without throwing', async () => {
    api.list.mockReturnValue(throwError(() => new Error('boom')));

    await store.load();

    expect(store.failed()).toBe(true);
    expect(store.loading()).toBe(false);
  });

  it('loads only once through ensureLoaded', async () => {
    api.list.mockReturnValue(of([]));

    await store.ensureLoaded();
    await store.ensureLoaded();

    expect(api.list).toHaveBeenCalledTimes(1);
  });

  it('adds a created conversation at the top', async () => {
    api.list.mockReturnValue(of([summary('1', 'First', 2)]));
    await store.load();
    api.create.mockReturnValue(of(summary('2', 'New conversation')));

    const created = await store.create();

    expect(created.id).toBe('2');
    expect(store.items().map((item) => item.id)).toEqual(['2', '1']);
  });

  it('replaces a renamed conversation', async () => {
    api.list.mockReturnValue(of([summary('1', 'First')]));
    await store.load();
    api.rename.mockReturnValue(of(summary('1', 'Renamed')));

    await store.rename('1', 'Renamed');

    expect(store.items()).toHaveLength(1);
    expect(store.items()[0]?.title).toBe('Renamed');
  });

  it('removes a deleted conversation', async () => {
    api.list.mockReturnValue(of([summary('1', 'First'), summary('2', 'Second', 1)]));
    await store.load();
    api.remove.mockReturnValue(of(undefined));

    await store.remove('1');

    expect(store.items().map((item) => item.id)).toEqual(['2']);
  });

  it('forgets the list when the session ends, even if a load is still running', async () => {
    api.list.mockReturnValue(of([summary('1', 'First')]));
    await store.load();

    let release: (items: ConversationSummary[]) => void = () => undefined;
    api.list.mockReturnValue(
      new Observable<ConversationSummary[]>((subscriber) => {
        release = (items) => {
          subscriber.next(items);
          subscriber.complete();
        };
      }),
    );
    const pending = store.load();

    patchState(TestBed.inject(AuthStore) as never, { status: 'anonymous' } as never);
    TestBed.tick();
    release([summary('late', 'Late')]);
    await pending;

    expect(store.items()).toEqual([]);
    expect(store.loaded()).toBe(false);
  });

  it('keeps the list when a deletion fails', async () => {
    api.list.mockReturnValue(of([summary('1', 'First')]));
    await store.load();
    api.remove.mockReturnValue(throwError(() => new Error('nope')));

    await expect(store.remove('1')).rejects.toThrow();

    expect(store.items()).toHaveLength(1);
  });
});
