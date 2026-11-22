import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { of, throwError } from 'rxjs';
import type { DocumentItem, DocumentPage } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { ApiProblemError } from '../../../core/http/problem';
import { provideApiTesting } from '../../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import type { DocumentStreamEvent } from '../data/document-events.service';
import { DocumentEventsService } from '../data/document-events.service';
import { DocumentUploader } from '../data/document-uploader';
import { DocumentsApi } from '../data/documents.api';
import { DocumentsStore } from './documents.store';

function document(id: string, status: DocumentItem['status'] = 'Ready'): DocumentItem {
  return {
    id,
    fileName: `${id}.pdf`,
    contentType: 'application/pdf',
    sizeBytes: 1000,
    status,
    errorMessage: null,
    pageCount: 1,
    chunkCount: 1,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
  } as DocumentItem;
}

function page(items: DocumentItem[], overrides: Partial<DocumentPage> = {}): DocumentPage {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: items.length > 0 ? 1 : 0,
    ...overrides,
  } as DocumentPage;
}

const pdf = (name: string, size = 1000) => new File([new Uint8Array(size)], name);

describe('DocumentsStore', () => {
  const api = { list: vi.fn(), remove: vi.fn() };
  const uploader = { upload: vi.fn() };
  const events = { stream: vi.fn() };
  let store: InstanceType<typeof DocumentsStore>;

  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    uploader.upload.mockReset();
    events.stream.mockReset();
    api.list.mockReturnValue(of(page([document('a')])));

    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: DocumentsApi, useValue: api },
        { provide: DocumentUploader, useValue: uploader },
        { provide: DocumentEventsService, useValue: events },
      ],
    });
    store = TestBed.inject(DocumentsStore);
  });

  afterEach(() => store.stopLive());

  it('loads the first page', async () => {
    await store.load();

    expect(api.list).toHaveBeenCalledWith({ page: 1, pageSize: 20, status: null });
    expect(store.items().map((item) => item.id)).toEqual(['a']);
    expect(store.loaded()).toBe(true);
  });

  it('reports a failed load', async () => {
    api.list.mockReturnValue(throwError(() => new Error('down')));

    await store.load();

    expect(store.failed()).toBe(true);
    expect(store.loading()).toBe(false);
  });

  it('filters by status and goes back to the first page', async () => {
    await store.setPage(3);
    await store.setStatus('Failed');

    expect(api.list).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, status: 'Failed' });
    expect(store.isFiltered()).toBe(true);
  });

  it('falls back to the last page when the current one becomes empty', async () => {
    api.list
      .mockReturnValueOnce(of(page([], { page: 3, totalCount: 40, totalPages: 2 })))
      .mockReturnValueOnce(of(page([document('z')], { page: 2, totalCount: 40, totalPages: 2 })));

    await store.setPage(3);

    expect(store.page()).toBe(2);
    expect(store.items().map((item) => item.id)).toEqual(['z']);
  });

  it('removes a document and refreshes the list', async () => {
    await store.load();
    api.remove.mockReturnValue(of(undefined));
    api.list.mockReturnValue(of(page([])));

    await store.remove('a');

    expect(api.remove).toHaveBeenCalledWith('a');
    expect(store.items()).toEqual([]);
  });

  describe('uploads', () => {
    it('rejects invalid files without calling the server', () => {
      store.upload([pdf('image.png'), pdf('empty.pdf', 0)]);

      expect(uploader.upload).not.toHaveBeenCalled();
      expect(store.uploads().map((upload) => upload.state)).toEqual(['failed', 'failed']);
      expect(store.uploads()[0]?.error).toContain('PDF');
    });

    it('uploads valid files and reloads the list', async () => {
      uploader.upload.mockImplementation(async (_file: File, onProgress: (n: number) => void) => {
        onProgress(50);
        return document('new', 'Pending');
      });

      store.upload([pdf('one.pdf')]);
      await vi.waitFor(() => expect(store.uploads()[0]?.state).toBe('done'));

      expect(store.uploads()[0]?.progress).toBe(100);
      expect(api.list).toHaveBeenCalled();
    });

    it('limits the number of parallel uploads', async () => {
      const releases: (() => void)[] = [];
      uploader.upload.mockImplementation(
        () =>
          new Promise((resolve) => {
            releases.push(() => resolve(document('x')));
          }),
      );

      store.upload([pdf('1.pdf'), pdf('2.pdf'), pdf('3.pdf'), pdf('4.pdf'), pdf('5.pdf')]);

      expect(uploader.upload).toHaveBeenCalledTimes(3);
      expect(store.uploads().filter((u) => u.state === 'queued')).toHaveLength(2);

      releases[0]?.();
      await vi.waitFor(() => expect(uploader.upload).toHaveBeenCalledTimes(4));
    });

    it('keeps the server reason when an upload fails', async () => {
      uploader.upload.mockRejectedValue(
        new ApiProblemError(409, { code: 'documents.quota_exceeded' } as never),
      );

      store.upload([pdf('one.pdf')]);
      await vi.waitFor(() => expect(store.uploads()[0]?.state).toBe('failed'));

      expect(store.uploads()[0]?.error).toContain('maximum number of documents');
    });

    it('cancels a queued upload and aborts a running one', async () => {
      uploader.upload.mockImplementation(
        (_file: File, _progress: unknown, signal: AbortSignal) =>
          new Promise((_resolve, reject) => {
            signal.addEventListener('abort', () => reject(new DOMException('x', 'AbortError')));
          }),
      );

      store.upload([pdf('1.pdf'), pdf('2.pdf'), pdf('3.pdf'), pdf('4.pdf')]);
      const [first, , , queued] = store.uploads();

      store.cancelUpload(queued?.id ?? -1);
      store.cancelUpload(first?.id ?? -1);

      await vi.waitFor(() => expect(store.uploads()[0]?.state).toBe('cancelled'));
      expect(store.uploads()[3]?.state).toBe('cancelled');
    });

    it('clears finished uploads only', () => {
      store.upload([pdf('image.png')]);
      expect(store.hasFinishedUploads()).toBe(true);

      store.clearFinishedUploads();

      expect(store.uploads()).toEqual([]);
    });
  });

  describe('live updates', () => {
    function feed(...streamEvents: DocumentStreamEvent[]) {
      events.stream.mockImplementation(async function* (signal: AbortSignal) {
        for (const event of streamEvents) {
          yield event;
        }
        await new Promise<void>((resolve) => signal.addEventListener('abort', () => resolve()));
      });
    }

    it('patches the status of a listed document', async () => {
      await store.load();
      feed(
        { type: 'open' },
        {
          type: 'status',
          documentId: 'a',
          status: 'Processing',
          errorMessage: null,
          updatedAt: '2030-02-01T00:00:00Z',
        },
      );

      store.startLive();

      await vi.waitFor(() => expect(store.items()[0]?.status).toBe('Processing'));
      expect(store.live()).toBe('live');
    });

    it('reloads when a document reaches a final status', async () => {
      await store.load();
      api.list.mockClear();
      feed({
        type: 'status',
        documentId: 'a',
        status: 'Ready',
        errorMessage: null,
        updatedAt: '2030-02-01T00:00:00Z',
      });

      store.startLive();

      await vi.waitFor(() => expect(api.list).toHaveBeenCalled());
    });

    it('reloads when an unknown document changes', async () => {
      await store.load();
      api.list.mockClear();
      feed({
        type: 'status',
        documentId: 'unknown',
        status: 'Pending',
        errorMessage: null,
        updatedAt: '',
      });

      store.startLive();

      await vi.waitFor(() => expect(api.list).toHaveBeenCalled());
    });

    it('goes idle when stopped', () => {
      feed({ type: 'open' });
      store.startLive();

      store.stopLive();

      expect(store.live()).toBe('idle');
    });
  });

  it('forgets everything when the session ends', async () => {
    await store.load();

    patchState(TestBed.inject(AuthStore) as never, { status: 'anonymous' } as never);
    TestBed.tick();

    expect(store.items()).toEqual([]);
    expect(store.loaded()).toBe(false);
  });
});
