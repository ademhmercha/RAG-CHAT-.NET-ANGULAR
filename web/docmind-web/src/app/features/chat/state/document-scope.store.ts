import { computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import type { DocumentItem } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { DocumentsApi } from '../../documents/data/documents.api';

interface ScopeState {
  documents: DocumentItem[];
  selectedIds: string[];
  loading: boolean;
  loaded: boolean;
}

const initialState: ScopeState = {
  documents: [],
  selectedIds: [],
  loading: false,
  loaded: false,
};

export const SCOPE_PAGE_SIZE = 100;

export const DocumentScopeStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ documents, selectedIds }) => ({
    selectedCount: computed(() => selectedIds().length),
    hasDocuments: computed(() => documents().length > 0),
    requestIds: computed(() => (selectedIds().length > 0 ? selectedIds() : null)),
  })),
  withMethods((store) => {
    const api = inject(DocumentsApi);

    let epoch = 0;

    const refresh = async (): Promise<void> => {
      const current = epoch;
      patchState(store, { loading: true });

      try {
        const page = await firstValueFrom(
          api.list({ page: 1, pageSize: SCOPE_PAGE_SIZE, status: 'Ready' }),
        );
        const known = new Set(page.items.map((item) => item.id));

        if (current === epoch) {
          patchState(store, {
            documents: page.items,
            selectedIds: store.selectedIds().filter((id) => known.has(id)),
            loaded: true,
          });
        }
      } catch {
        // The selector keeps what it had; the next opening retries.
      } finally {
        if (current === epoch) {
          patchState(store, { loading: false });
        }
      }
    };

    return {
      refresh,

      async ensureLoaded(): Promise<void> {
        if (!store.loaded() && !store.loading()) {
          await refresh();
        }
      },

      toggle(id: string): void {
        const selected = store.selectedIds();
        patchState(store, {
          selectedIds: selected.includes(id)
            ? selected.filter((value) => value !== id)
            : [...selected, id],
        });
      },

      clear(): void {
        patchState(store, { selectedIds: [] });
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
