import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { DocumentItem, DocumentPage } from '../../../core/api/api-types';
import { provideApiTesting } from '../../../../testing/api-testing';
import { DocumentsApi } from '../../documents/data/documents.api';
import { DocumentScopeStore } from './document-scope.store';

const doc = (id: string) => ({ id, fileName: `${id}.pdf`, status: 'Ready' }) as DocumentItem;
const page = (...ids: string[]) => ({ items: ids.map(doc) }) as DocumentPage;

describe('DocumentScopeStore', () => {
  const api = { list: vi.fn() };
  let store: InstanceType<typeof DocumentScopeStore>;

  beforeEach(() => {
    api.list.mockReset();
    TestBed.configureTestingModule({
      providers: [provideApiTesting(), provideRouter([]), { provide: DocumentsApi, useValue: api }],
    });
    store = TestBed.inject(DocumentScopeStore);
  });

  it('loads only the ready documents', async () => {
    api.list.mockReturnValue(of(page('a', 'b')));

    await store.ensureLoaded();
    await store.ensureLoaded();

    expect(api.list).toHaveBeenCalledOnce();
    expect(api.list).toHaveBeenCalledWith({ page: 1, pageSize: 100, status: 'Ready' });
    expect(store.documents()).toHaveLength(2);
  });

  it('sends nothing when no document is selected', () => {
    expect(store.requestIds()).toBeNull();
  });

  it('toggles and clears the selection', async () => {
    api.list.mockReturnValue(of(page('a', 'b')));
    await store.refresh();

    store.toggle('a');
    store.toggle('b');
    store.toggle('a');

    expect(store.requestIds()).toEqual(['b']);

    store.clear();

    expect(store.requestIds()).toBeNull();
  });

  it('forgets selected documents that are no longer ready', async () => {
    api.list.mockReturnValue(of(page('a', 'b')));
    await store.refresh();
    store.toggle('a');
    store.toggle('b');

    api.list.mockReturnValue(of(page('b')));
    await store.refresh();

    expect(store.selectedIds()).toEqual(['b']);
  });

  it('keeps its state when the refresh fails', async () => {
    api.list.mockReturnValue(of(page('a')));
    await store.refresh();
    api.list.mockReturnValue(throwError(() => new Error('down')));

    await store.refresh();

    expect(store.documents()).toHaveLength(1);
    expect(store.loading()).toBe(false);
  });
});
