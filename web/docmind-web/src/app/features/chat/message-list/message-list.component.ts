import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import type { ElementRef } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import type { Citation } from '../../../core/api/api-types';
import { MessageItemComponent } from '../message-item/message-item.component';
import type { CitationSelection } from '../message-item/message-item.component';
import type { ChatMessage, ToolActivity } from '../state/chat.store';

const STICK_THRESHOLD_PX = 96;

@Component({
  selector: 'dm-message-list',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe, MessageItemComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './message-list.component.html',
  styleUrl: './message-list.component.scss',
})
export class MessageListComponent {
  private readonly injector = inject(Injector);

  readonly messages = input.required<readonly ChatMessage[]>();
  readonly tool = input<ToolActivity | null>(null);
  readonly resetKey = input<string | null>(null);
  readonly activeCitation = input<Citation | null>(null);

  readonly citationSelected = output<CitationSelection>();

  private readonly scroller = viewChild.required<ElementRef<HTMLElement>>('scroller');

  protected readonly pinned = signal(true);

  constructor() {
    effect(() => {
      this.resetKey();
      untracked(() => this.pinned.set(true));
    });

    effect(() => {
      const messages = this.messages();
      const last = messages.at(-1);

      untracked(() => {
        if (last?.status === 'streaming' && last.content === '') {
          this.pinned.set(true);
        }

        if (this.pinned()) {
          afterNextRender(() => this.scrollToBottom(false), { injector: this.injector });
        }
      });
    });
  }

  protected onScroll(): void {
    const element = this.scroller().nativeElement;
    const distance = element.scrollHeight - element.scrollTop - element.clientHeight;
    this.pinned.set(distance <= STICK_THRESHOLD_PX);
  }

  protected jumpToLatest(): void {
    this.pinned.set(true);
    this.scrollToBottom(true);
  }

  private scrollToBottom(smooth: boolean): void {
    const element = this.scroller().nativeElement;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    element.scrollTo({
      top: element.scrollHeight,
      behavior: smooth && !reduced ? 'smooth' : 'auto',
    });
  }
}
