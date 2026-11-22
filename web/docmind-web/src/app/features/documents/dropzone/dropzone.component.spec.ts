import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import { DropzoneComponent } from './dropzone.component';

function dragEvent(type: string, files: File[] = []): DragEvent {
  const event = new Event(type, { bubbles: true, cancelable: true }) as DragEvent;
  Object.defineProperty(event, 'dataTransfer', {
    value: { types: ['Files'], files },
  });
  return event;
}

describe('DropzoneComponent', () => {
  let fixture: ComponentFixture<DropzoneComponent>;
  let received: File[][];

  const zone = () => fixture.nativeElement.querySelector('.dropzone') as HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] });
    fixture = TestBed.createComponent(DropzoneComponent);
    received = [];
    fixture.componentInstance.filesSelected.subscribe((files) => received.push(files));
    fixture.detectChanges();
  });

  it('emits dropped files', () => {
    const file = new File(['x'], 'a.pdf');

    zone().dispatchEvent(dragEvent('dragenter'));
    zone().dispatchEvent(dragEvent('drop', [file]));
    fixture.detectChanges();

    expect(received).toEqual([[file]]);
    expect(zone().classList).not.toContain('dragging');
  });

  it('highlights while files are dragged over, even across child elements', () => {
    zone().dispatchEvent(dragEvent('dragenter'));
    zone().dispatchEvent(dragEvent('dragenter'));
    zone().dispatchEvent(dragEvent('dragleave'));
    fixture.detectChanges();

    expect(zone().classList).toContain('dragging');

    zone().dispatchEvent(dragEvent('dragleave'));
    fixture.detectChanges();

    expect(zone().classList).not.toContain('dragging');
  });

  it('emits files chosen through the file picker', () => {
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['x'], 'b.docx');
    Object.defineProperty(input, 'files', { value: [file], configurable: true });

    input.dispatchEvent(new Event('change'));

    expect(received).toEqual([[file]]);
  });

  it('restricts the picker to supported types', () => {
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;

    expect(input.accept).toBe('.pdf,.docx');
    expect(input.multiple).toBe(true);
  });
});
