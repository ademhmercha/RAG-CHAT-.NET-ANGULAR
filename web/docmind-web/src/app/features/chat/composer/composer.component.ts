import { TextFieldModule } from '@angular/cdk/text-field';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { ElementRef } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';

export const MESSAGE_MAX_LENGTH = 8000;

@Component({
  selector: 'dm-composer',
  imports: [TextFieldModule, MatButtonModule, MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './composer.component.html',
  styleUrl: './composer.component.scss',
})
export class ComposerComponent {
  readonly streaming = input(false);
  readonly disabled = input(false);

  readonly submitted = output<string>();
  readonly stopped = output<void>();

  protected readonly maxLength = MESSAGE_MAX_LENGTH;
  protected readonly draft = signal('');
  protected readonly canSend = computed(
    () => this.draft().trim().length > 0 && !this.streaming() && !this.disabled(),
  );

  private readonly field = viewChild.required<ElementRef<HTMLTextAreaElement>>('field');

  focus(): void {
    this.field().nativeElement.focus();
  }

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.send();
    }
  }

  protected send(): void {
    if (!this.canSend()) {
      return;
    }

    this.submitted.emit(this.draft().trim());
    this.draft.set('');
    this.field().nativeElement.value = '';
  }
}
