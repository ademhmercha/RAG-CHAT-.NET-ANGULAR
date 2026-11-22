import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DomSanitizer } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { Citation } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { copyText } from '../../../core/services/clipboard';
import { NotificationService } from '../../../core/services/notification.service';
import { MarkdownService } from '../../../shared/markdown/markdown.service';
import type { ChatMessage, ToolActivity } from '../state/chat.store';

export interface CitationSelection {
  readonly citation: Citation;
  readonly index: number;
}

@Component({
  selector: 'dm-message-item',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(click)': 'onContentClick($event)' },
  templateUrl: './message-item.component.html',
  styleUrl: './message-item.component.scss',
})
export class MessageItemComponent {
  private readonly markdown = inject(MarkdownService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly transloco = inject(TranslocoService);
  private readonly notifications = inject(NotificationService);

  protected readonly auth = inject(AuthStore);

  readonly message = input.required<ChatMessage>();
  readonly tool = input<ToolActivity | null>(null);
  readonly activeCitation = input<Citation | null>(null);

  readonly citationSelected = output<CitationSelection>();

  private readonly language = toSignal(this.transloco.langChanges$);

  protected readonly isUser = computed(() => this.message().role === 'user');
  protected readonly isStreaming = computed(() => this.message().status === 'streaming');

  protected readonly html = computed(() => {
    this.language();
    const { content } = this.message();
    const rendered = this.markdown.render(content, this.transloco.translate('common.copy'));

    // The markup is produced by marked and sanitized by DOMPurify before being trusted here.
    return this.sanitizer.bypassSecurityTrustHtml(rendered);
  });

  protected readonly toolKey = computed(() => {
    const tool = this.tool();
    if (!tool) {
      return null;
    }

    const family = tool.name === 'search_documents' ? tool.name : 'generic';
    return `chat.tools.${family}.${tool.status}`;
  });

  protected isActive(citation: Citation): boolean {
    const active = this.activeCitation();
    return (
      active !== null &&
      active.documentId === citation.documentId &&
      active.page === citation.page &&
      active.excerpt === citation.excerpt
    );
  }

  protected async copyAnswer(): Promise<void> {
    if (await copyText(this.message().content)) {
      this.notifications.info('chat.message.copied');
    }
  }

  protected async onContentClick(event: Event): Promise<void> {
    const button = (event.target as HTMLElement).closest<HTMLElement>('[data-copy]');
    if (!button) {
      return;
    }

    const code = button.closest('.code-block')?.querySelector('code')?.textContent ?? '';
    if (await copyText(code)) {
      this.notifications.info('chat.message.codeCopied');
    }
  }
}
