import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { SkeletonComponent } from '../../shared/skeleton/skeleton.component';
import { CitationPanelComponent } from './citation-panel/citation-panel.component';
import { ComposerComponent } from './composer/composer.component';
import { ConversationListComponent } from './conversation-list/conversation-list.component';
import { MessageListComponent } from './message-list/message-list.component';
import type { CitationSelection } from './message-item/message-item.component';
import { ScopeSelectorComponent } from './scope-selector/scope-selector.component';
import { ChatStore } from './state/chat.store';
import { ConversationsStore } from './state/conversations.store';
import { DocumentScopeStore } from './state/document-scope.store';

const SUGGESTION_KEYS = ['summarize', 'decisions', 'dates', 'compare'] as const;
const DEFAULT_TITLE = 'New conversation';

@Component({
  selector: 'dm-chat-page',
  imports: [
    MatButtonModule,
    MatIconModule,
    RouterLink,
    TranslocoPipe,
    EmptyStateComponent,
    SkeletonComponent,
    CitationPanelComponent,
    ComposerComponent,
    ConversationListComponent,
    MessageListComponent,
    ScopeSelectorComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat.page.html',
  styleUrl: './chat.page.scss',
})
export class ChatPage {
  protected readonly chat = inject(ChatStore);
  private readonly conversations = inject(ConversationsStore);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly scope = inject(DocumentScopeStore);

  readonly conversationId = input<string | undefined>();

  protected readonly selection = signal<CitationSelection | null>(null);
  protected readonly announcement = signal('');

  private readonly composer = viewChild(ComposerComponent);

  protected readonly listOpen = signal(false);
  protected readonly suggestionKeys = SUGGESTION_KEYS;

  protected readonly title = computed(() => {
    const id = this.chat.conversationId();
    const title = this.conversations.items().find((item) => item.id === id)?.title;

    if (!title) {
      return null;
    }

    return title === DEFAULT_TITLE ? this.transloco.translate('chat.defaultTitle') : title;
  });

  constructor() {
    void this.conversations.ensureLoaded();

    effect(() => {
      const id = this.conversationId();
      untracked(() => {
        if (id) {
          void this.chat.open(id);
        } else {
          this.chat.startNew();
        }
      });
    });

    effect(() => {
      this.chat.conversationId();
      untracked(() => this.selection.set(null));
    });

    let wasStreaming = false;
    effect(() => {
      const streaming = this.chat.streaming();

      untracked(() => {
        if (streaming) {
          this.announcement.set(this.transloco.translate('chat.message.responding'));
        } else if (wasStreaming) {
          this.announcement.set(this.transloco.translate('chat.message.ready'));
        }
        wasStreaming = streaming;
      });
    });

    effect(() => {
      const id = this.chat.conversationId();
      const streaming = this.chat.streaming();

      untracked(() => {
        if (id && streaming && this.conversationId() !== id) {
          void this.router.navigate(['/chat', id], { replaceUrl: true });
        }
      });
    });
  }

  protected submit(text: string): void {
    void this.chat.send(text, this.scope.requestIds());
  }

  protected ask(key: string): void {
    this.submit(this.transloco.translate(`chat.suggestions.${key}`));
  }

  protected onDeleted(id: string): void {
    if (id === this.chat.conversationId()) {
      void this.router.navigateByUrl('/chat');
    }
  }

  protected closeList(): void {
    this.listOpen.set(false);
    this.composer()?.focus();
  }
}
