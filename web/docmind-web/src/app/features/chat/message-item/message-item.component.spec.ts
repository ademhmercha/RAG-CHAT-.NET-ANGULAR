import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideApiTesting } from '../../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import type { ChatMessage } from '../state/chat.store';
import type { CitationSelection } from './message-item.component';
import { MessageItemComponent } from './message-item.component';

function message(overrides: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id: 'm1',
    role: 'assistant',
    content: '',
    citations: [],
    createdAt: '2030-01-01T00:00:00Z',
    status: 'complete',
    error: null,
    ...overrides,
  };
}

describe('MessageItemComponent', () => {
  let fixture: ComponentFixture<MessageItemComponent>;
  const root = () => fixture.nativeElement as HTMLElement;

  function render(value: ChatMessage, tool: { name: string; status: string } | null = null) {
    fixture.componentRef.setInput('message', value);
    fixture.componentRef.setInput('tool', tool);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiTesting(), provideTranslocoTesting()] });
    fixture = TestBed.createComponent(MessageItemComponent);
  });

  it('renders the assistant answer as sanitized markdown', () => {
    render(message({ content: '**Bold** <script>alert(1)</script>' }));

    expect(root().querySelector('.dm-markdown strong')?.textContent).toBe('Bold');
    expect(root().querySelector('script')).toBeNull();
  });

  it('shows user text verbatim, without markdown', () => {
    render(message({ role: 'user', content: '**not bold**' }));

    expect(root().querySelector('.user-text')?.textContent).toBe('**not bold**');
    expect(root().querySelector('.dm-markdown')).toBeNull();
  });

  it('shows a thinking indicator before the first token', () => {
    render(message({ status: 'streaming' }));

    expect(root().querySelector('.thinking')?.textContent).toContain('Thinking');
  });

  it('announces the running tool', () => {
    render(message({ status: 'streaming' }), { name: 'search_documents', status: 'running' });

    expect(root().querySelector('.tool')?.textContent).toContain('Searching your documents');
    expect(root().querySelector('.thinking')).toBeNull();
  });

  it('reports a failed answer', () => {
    render(message({ content: 'Par', status: 'failed', error: 'Service unavailable' }));

    expect(root().querySelector('[role="alert"]')?.textContent).toContain('Service unavailable');
  });

  it('lists citations and emits the selected one', () => {
    const citation = { documentId: 'd1', documentName: 'plan.pdf', page: 4, excerpt: 'text' };
    const selections: CitationSelection[] = [];
    fixture.componentInstance.citationSelected.subscribe((value) => selections.push(value));
    render(message({ content: 'Answer', citations: [citation] }));

    const chip = root().querySelector<HTMLButtonElement>('button.citation');
    chip?.click();

    expect(chip?.textContent).toContain('plan.pdf');
    expect(chip?.textContent).toContain('p. 4');
    expect(selections).toEqual([{ citation, index: 0 }]);
  });
});
