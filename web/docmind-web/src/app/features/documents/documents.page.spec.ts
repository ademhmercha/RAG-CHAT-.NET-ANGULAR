import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { DocumentItem, DocumentPage } from '../../core/api/api-types';
import { provideApiTesting } from '../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../testing/transloco-testing';
import { ConfirmService } from '../../shared/confirm-dialog/confirm-dialog.component';
import { DocumentEventsService } from './data/document-events.service';
import { DocumentUploader } from './data/document-uploader';
import { DocumentsApi } from './data/documents.api';
import { DocumentsPage } from './documents.page';

function document(id: string, status: DocumentItem['status'], errorMessage: string | null = null) {
  return {
    id,
    fileName: `${id}.pdf`,
    contentType: 'application/pdf',
    sizeBytes: 2048,
    status,
    errorMessage,
    pageCount: 3,
    chunkCount: 9,
    createdAt: '2030-01-01T10:00:00Z',
    updatedAt: '2030-01-01T10:00:00Z',
  } as DocumentItem;
}

const page = (items: DocumentItem[]) =>
  ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 }) as DocumentPage;

describe('DocumentsPage', () => {
  const api = { list: vi.fn(), remove: vi.fn() };
  const confirm = { confirm: vi.fn() };
  let fixture: ComponentFixture<DocumentsPage>;

  const root = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(root().querySelectorAll('[data-testid="document-row"]'));

  async function create(items: DocumentItem[]) {
    api.list.mockReturnValue(of(page(items)));
    fixture = TestBed.createComponent(DocumentsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    api.list.mockReset();
    api.remove.mockReset();
    confirm.confirm.mockReset();

    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: DocumentsApi, useValue: api },
        { provide: ConfirmService, useValue: confirm },
        { provide: DocumentUploader, useValue: { upload: vi.fn() } },
        {
          provide: DocumentEventsService,
          useValue: {
            stream: async function* (signal: AbortSignal) {
              yield { type: 'open' };
              await new Promise<void>((resolve) =>
                signal.addEventListener('abort', () => resolve()),
              );
            },
          },
        },
      ],
    });
  });

  it('shows the documents with their status', async () => {
    await create([document('alpha', 'Ready'), document('beta', 'Processing')]);

    expect(rows()).toHaveLength(2);
    expect(rows()[0]?.textContent).toContain('alpha.pdf');
    expect(rows()[0]?.textContent).toContain('Ready');
    expect(rows()[1]?.textContent).toContain('Processing');
  });

  it('explains why a document failed', async () => {
    await create([document('broken', 'Failed', 'Corrupt PDF')]);

    expect(rows()[0]?.textContent).toContain('Processing failed: Corrupt PDF');
  });

  it('shows an empty state without documents', async () => {
    await create([]);

    expect(root().textContent).toContain('No documents yet');
    expect(root().querySelector('table')).toBeNull();
  });

  it('asks for confirmation before deleting', async () => {
    await create([document('alpha', 'Ready')]);
    confirm.confirm.mockResolvedValue(false);

    root().querySelector<HTMLButtonElement>('button[aria-label^="Delete"]')?.click();
    await fixture.whenStable();

    expect(confirm.confirm).toHaveBeenCalledOnce();
    expect(api.remove).not.toHaveBeenCalled();
  });

  it('deletes after confirmation', async () => {
    await create([document('alpha', 'Ready')]);
    confirm.confirm.mockResolvedValue(true);
    api.remove.mockReturnValue(of(undefined));
    api.list.mockReturnValue(of(page([])));

    root().querySelector<HTMLButtonElement>('button[aria-label^="Delete"]')?.click();
    await vi.waitFor(() => expect(api.remove).toHaveBeenCalledWith('alpha'));
  });

  it('filters by status', async () => {
    await create([document('alpha', 'Ready')]);

    const toggles = Array.from(
      root().querySelectorAll<HTMLButtonElement>('mat-button-toggle button'),
    );
    toggles.find((toggle) => toggle.textContent?.trim() === 'Failed')?.click();

    await vi.waitFor(() =>
      expect(api.list).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, status: 'Failed' }),
    );
  });
});
