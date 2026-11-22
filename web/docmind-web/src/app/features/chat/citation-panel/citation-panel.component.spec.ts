import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { DocumentItem } from '../../../core/api/api-types';
import { provideApiTesting } from '../../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import { DocumentsApi } from '../../documents/data/documents.api';
import { CitationPanelComponent } from './citation-panel.component';

const citation = {
  documentId: 'doc-1',
  documentName: 'contract.pdf',
  page: 7,
  excerpt: 'The parties agree to the following terms.',
};

describe('CitationPanelComponent', () => {
  const api = { get: vi.fn() };
  let fixture: ComponentFixture<CitationPanelComponent>;
  const root = () => fixture.nativeElement as HTMLElement;

  async function render() {
    fixture = TestBed.createComponent(CitationPanelComponent);
    fixture.componentRef.setInput('citation', citation);
    fixture.componentRef.setInput('index', 1);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    api.get.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: DocumentsApi, useValue: api },
      ],
    });
  });

  it('shows the excerpt, the page and the source document details', async () => {
    api.get.mockReturnValue(of({ pageCount: 12, sizeBytes: 2048 } as DocumentItem));

    await render();

    expect(root().textContent).toContain('contract.pdf');
    expect(root().textContent).toContain('p. 7');
    expect(root().textContent).toContain('12 pages in total');
    expect(root().querySelector('blockquote')?.textContent).toContain('agree to the following');
    expect(root().querySelector('.badge')?.textContent).toBe('2');
    expect(api.get).toHaveBeenCalledWith('doc-1', true);
  });

  it('keeps the excerpt readable when the document no longer exists', async () => {
    api.get.mockReturnValue(throwError(() => new Error('404')));

    await render();

    expect(root().textContent).toContain('no longer available');
    expect(root().querySelector('blockquote')).not.toBeNull();
  });

  it('emits when closed', async () => {
    api.get.mockReturnValue(of({ pageCount: 1, sizeBytes: 1 } as DocumentItem));
    await render();
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);

    root().querySelector<HTMLButtonElement>('button.close')?.click();

    expect(closed).toBe(1);
  });
});
