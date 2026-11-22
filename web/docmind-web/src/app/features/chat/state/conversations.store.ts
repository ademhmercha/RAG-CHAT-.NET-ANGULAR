import { InjectionToken, computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import type { ConversationSummary } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { ConversationsApi } from '../data/conversations.api';
import { groupConversations } from '../util/group-conversations';

interface ConversationsState {
  items: ConversationSummary[];
  loading: boolean;
  loaded: boolean;
  failed: boolean;
  query: string;
}

const initialState: ConversationsState = {
  items: [],
  loading: false,
  loaded: false,
  failed: false,
  query: '',
};

export const CONVERSATIONS_NOW = new InjectionToken<() => number>('CONVERSATIONS_NOW', {
  providedIn: 'root',
  factory: () => () => Date.now(),
});

const byRecency = (a: ConversationSummary, b: ConversationSummary): number =>
  Date.parse(b.updatedAt) - Date.parse(a.updatedAt);

export const ConversationsStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ items, query }) => {
    const now = inject(CONVERSATIONS_NOW);
    const filtered = computed(() => {
      const needle = query().trim().toLowerCase();
      return needle ? items().filter((item) => item.title.toLowerCase().includes(needle)) : items();
    });

    return {
      filtered,
      groups: computed(() => groupConversations(filtered(), now())),
      isEmpty: computed(() => items().length === 0),
    };
  }),
  withMethods((store) => {
    const api = inject(ConversationsApi);

    const upsert = (item: ConversationSummary): void =>
      patchState(store, {
        items: [item, ...store.items().filter((existing) => existing.id !== item.id)].sort(
          byRecency,
        ),
      });

    let epoch = 0;

    const load = async (): Promise<void> => {
      const current = epoch;
      patchState(store, { loading: true, failed: false });

      try {
        const items = await firstValueFrom(api.list());
        if (current === epoch) {
          patchState(store, { items: [...items].sort(byRecency), loaded: true });
        }
      } catch {
        if (current === epoch) {
          patchState(store, { failed: true });
        }
      } finally {
        if (current === epoch) {
          patchState(store, { loading: false });
        }
      }
    };

    return {
      load,

      async ensureLoaded(): Promise<void> {
        if (!store.loaded() && !store.loading()) {
          await load();
        }
      },

      async refresh(): Promise<void> {
        const current = epoch;

        try {
          const items = await firstValueFrom(api.list());
          if (current === epoch) {
            patchState(store, { items: [...items].sort(byRecency), loaded: true, failed: false });
          }
        } catch {
          // The visible list stays as it was; the next explicit load reports the failure.
        }
      },

      async create(title: string | null = null): Promise<ConversationSummary> {
        const created = await firstValueFrom(api.create(title));
        upsert(created);
        return created;
      },

      async rename(id: string, title: string): Promise<void> {
        upsert(await firstValueFrom(api.rename(id, title)));
      },

      async remove(id: string): Promise<void> {
        await firstValueFrom(api.remove(id));
        patchState(store, { items: store.items().filter((item) => item.id !== id) });
      },

      setQuery(query: string): void {
        patchState(store, { query });
      },

      reset(): void {
        epoch++;
        patchState(store, initialState);
      },
    };
  }),
  withHooks({
    onInit(store) {
      const auth = inject(AuthStore);

      effect(() => {
        if (auth.status() === 'anonymous') {
          store.reset();
        }
      });
    },
  }),
);
