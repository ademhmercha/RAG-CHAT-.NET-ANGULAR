import { computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { DocumentItem, DocumentStatus } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { ProblemMessages } from '../../../core/http/problem';
import { DocumentEventsService } from '../data/document-events.service';
import type { DocumentStatusEvent } from '../data/document-events.service';
import { DocumentUploader } from '../data/document-uploader';
import { DocumentsApi } from '../data/documents.api';
import { MAX_UPLOAD_BYTES, validateUpload } from '../util/upload-validation';

export type UploadState = 'queued' | 'uploading' | 'done' | 'failed' | 'cancelled';
export type LiveState = 'idle' | 'connecting' | 'live' | 'offline';

export interface UploadItem {
  readonly id: number;
  readonly fileName: string;
  readonly sizeBytes: number;
  readonly progress: number;
  readonly state: UploadState;
  readonly error: string | null;
}

interface DocumentsState {
  items: DocumentItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  status: DocumentStatus | null;
  loading: boolean;
  loaded: boolean;
  failed: boolean;
  uploads: UploadItem[];
  live: LiveState;
}

const initialState: DocumentsState = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  status: null,
  loading: false,
  loaded: false,
  failed: false,
  uploads: [],
  live: 'idle',
};

export const MAX_PARALLEL_UPLOADS = 3;
const RECONNECT_BASE_MS = 1000;
const RECONNECT_MAX_MS = 15_000;

function wait(milliseconds: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(resolve, milliseconds);
    signal.addEventListener(
      'abort',
      () => {
        clearTimeout(timer);
        resolve();
      },
      { once: true },
    );
  });
}

export const DocumentsStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ uploads, items, totalCount, status }) => ({
    activeUploads: computed(
      () => uploads().filter((u) => u.state === 'queued' || u.state === 'uploading').length,
    ),
    hasFinishedUploads: computed(() =>
      uploads().some((u) => u.state !== 'queued' && u.state !== 'uploading'),
    ),
    isEmpty: computed(() => items().length === 0 && totalCount() === 0),
    isFiltered: computed(() => status() !== null),
  })),
  withMethods((store) => {
    const api = inject(DocumentsApi);
    const uploader = inject(DocumentUploader);
    const events = inject(DocumentEventsService);
    const problems = inject(ProblemMessages);
    const transloco = inject(TranslocoService);

    let uploadSequence = 0;
    let liveController: AbortController | null = null;
    const pending: { file: File; id: number }[] = [];
    const controllers = new Map<number, AbortController>();
    let running = 0;
    let loadRequest = 0;

    const patchUpload = (id: number, change: Partial<UploadItem>): void =>
      patchState(store, {
        uploads: store.uploads().map((item) => (item.id === id ? { ...item, ...change } : item)),
      });

    const fetchPage = async (quiet: boolean): Promise<void> => {
      const request = ++loadRequest;
      patchState(store, quiet ? { failed: false } : { loading: true, failed: false });

      try {
        const result = await firstValueFrom(
          api.list({ page: store.page(), pageSize: store.pageSize(), status: store.status() }),
        );

        if (request !== loadRequest) {
          return;
        }

        const totalPages = result.totalPages ?? 0;
        if (result.items.length === 0 && result.page > 1 && totalPages > 0) {
          patchState(store, { page: totalPages });
          await fetchPage(quiet);
          return;
        }

        patchState(store, {
          items: result.items,
          page: result.page,
          totalCount: result.totalCount,
          totalPages,
          loaded: true,
        });
      } catch {
        if (request === loadRequest) {
          patchState(store, { failed: true });
        }
      } finally {
        if (request === loadRequest) {
          patchState(store, { loading: false });
        }
      }
    };

    const pump = (): void => {
      while (running < MAX_PARALLEL_UPLOADS && pending.length > 0) {
        const next = pending.shift();
        if (next) {
          running++;
          void runUpload(next.file, next.id).finally(() => {
            running--;
            pump();
          });
        }
      }
    };

    const runUpload = async (file: File, id: number): Promise<void> => {
      const controller = new AbortController();
      controllers.set(id, controller);
      patchUpload(id, { state: 'uploading', progress: 0 });

      try {
        await uploader.upload(file, (progress) => patchUpload(id, { progress }), controller.signal);
        patchUpload(id, { state: 'done', progress: 100 });
        void fetchPage(true);
      } catch (error) {
        if (controller.signal.aborted) {
          patchUpload(id, { state: 'cancelled' });
        } else {
          patchUpload(id, { state: 'failed', error: problems.describe(error) });
        }
      } finally {
        controllers.delete(id);
      }
    };

    const applyStatus = (event: DocumentStatusEvent): void => {
      const known = store.items().some((item) => item.id === event.documentId);
      if (!known) {
        void fetchPage(true);
        return;
      }

      patchState(store, {
        items: store.items().map((item) =>
          item.id === event.documentId
            ? {
                ...item,
                status: event.status,
                errorMessage: event.errorMessage,
                updatedAt: event.updatedAt || item.updatedAt,
              }
            : item,
        ),
      });

      if (event.status === 'Ready' || event.status === 'Failed' || store.status() !== null) {
        void fetchPage(true);
      }
    };

    const stopLive = (): void => {
      liveController?.abort();
      liveController = null;
      patchState(store, { live: 'idle' });
    };

    const startLive = (): void => {
      if (liveController) {
        return;
      }

      const controller = new AbortController();
      liveController = controller;
      patchState(store, { live: 'connecting' });

      void (async () => {
        let delay = RECONNECT_BASE_MS;
        let reconnecting = false;

        while (!controller.signal.aborted) {
          try {
            for await (const event of events.stream(controller.signal)) {
              if (event.type === 'open') {
                patchState(store, { live: 'live' });
                delay = RECONNECT_BASE_MS;
                if (reconnecting) {
                  void fetchPage(true);
                }
              } else {
                applyStatus(event);
              }
            }
          } catch {
            // The connection dropped; retry below unless the page was left.
          }

          if (controller.signal.aborted) {
            break;
          }

          reconnecting = true;
          patchState(store, { live: 'offline' });
          await wait(delay, controller.signal);
          delay = Math.min(delay * 2, RECONNECT_MAX_MS);
        }
      })();
    };

    const reset = (): void => {
      stopLive();
      pending.length = 0;
      controllers.forEach((controller) => controller.abort());
      loadRequest++;
      patchState(store, initialState);
    };

    return {
      load: () => fetchPage(false),
      refresh: () => fetchPage(true),
      startLive,
      stopLive,
      reset,

      async setStatus(status: DocumentStatus | null): Promise<void> {
        patchState(store, { status, page: 1 });
        await fetchPage(false);
      },

      async setPage(page: number, pageSize = store.pageSize()): Promise<void> {
        patchState(store, { page, pageSize });
        await fetchPage(false);
      },

      async remove(id: string): Promise<void> {
        await firstValueFrom(api.remove(id));
        patchState(store, { items: store.items().filter((item) => item.id !== id) });
        await fetchPage(true);
      },

      upload(files: readonly File[]): void {
        for (const file of files) {
          const id = ++uploadSequence;
          const rejection = validateUpload(file);
          const base = {
            id,
            fileName: file.name,
            sizeBytes: file.size,
            progress: 0,
          } as const;

          if (rejection) {
            patchState(store, {
              uploads: [
                ...store.uploads(),
                {
                  ...base,
                  state: 'failed',
                  error: transloco.translate(`documents.upload.errors.${rejection}`, {
                    maxSize: Math.round(MAX_UPLOAD_BYTES / (1024 * 1024)),
                  }),
                },
              ],
            });
            continue;
          }

          patchState(store, {
            uploads: [...store.uploads(), { ...base, state: 'queued', error: null }],
          });
          pending.push({ file, id });
        }

        pump();
      },

      cancelUpload(id: number): void {
        const queued = pending.findIndex((item) => item.id === id);
        if (queued !== -1) {
          pending.splice(queued, 1);
          patchUpload(id, { state: 'cancelled' });
          return;
        }

        controllers.get(id)?.abort();
      },

      dismissUpload(id: number): void {
        patchState(store, { uploads: store.uploads().filter((item) => item.id !== id) });
      },

      clearFinishedUploads(): void {
        patchState(store, {
          uploads: store
            .uploads()
            .filter((item) => item.state === 'queued' || item.state === 'uploading'),
        });
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
