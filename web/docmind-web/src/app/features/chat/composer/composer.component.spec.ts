import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import { ComposerComponent } from './composer.component';

describe('ComposerComponent', () => {
  let fixture: ComponentFixture<ComposerComponent>;
  let submitted: string[];
  let stopped: number;

  const field = () => fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
  const button = () => fixture.nativeElement.querySelector('button.action') as HTMLButtonElement;

  function type(value: string): void {
    field().value = value;
    field().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function press(init: KeyboardEventInit): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { cancelable: true, bubbles: true, ...init });
    field().dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] });
    fixture = TestBed.createComponent(ComposerComponent);
    submitted = [];
    stopped = 0;
    fixture.componentInstance.submitted.subscribe((text) => submitted.push(text));
    fixture.componentInstance.stopped.subscribe(() => stopped++);
    fixture.detectChanges();
  });

  it('disables sending while the draft is empty', () => {
    expect(button().disabled).toBe(true);

    type('   ');
    expect(button().disabled).toBe(true);

    type('Hello');
    expect(button().disabled).toBe(false);
  });

  it('submits on Enter and clears the field', () => {
    type('  Hello there ');

    const event = press({ key: 'Enter' });

    expect(event.defaultPrevented).toBe(true);
    expect(submitted).toEqual(['Hello there']);
    expect(field().value).toBe('');
  });

  it('keeps Shift+Enter for new lines', () => {
    type('Hello');

    const event = press({ key: 'Enter', shiftKey: true });

    expect(event.defaultPrevented).toBe(false);
    expect(submitted).toEqual([]);
  });

  it('does not submit while an IME composition is active', () => {
    type('こん');

    press({ key: 'Enter', isComposing: true });

    expect(submitted).toEqual([]);
  });

  it('offers a stop button while streaming', () => {
    fixture.componentRef.setInput('streaming', true);
    fixture.detectChanges();

    button().click();

    expect(button().classList).toContain('stop');
    expect(stopped).toBe(1);
  });

  it('refuses to send while streaming', () => {
    type('Hello');
    fixture.componentRef.setInput('streaming', true);
    fixture.detectChanges();

    press({ key: 'Enter' });

    expect(submitted).toEqual([]);
  });
});
