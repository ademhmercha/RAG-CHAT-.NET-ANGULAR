import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ConversationSummary } from '../../../core/api/api-types';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmService } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { ErrorStateComponent } from '../../../shared/error-state/error-state.component';
import { SkeletonComponent } from '../../../shared/skeleton/skeleton.component';
import { ConversationsStore } from '../state/conversations.store';
import { RenameDialogComponent } from './rename-dialog.component';

const DEFAULT_TITLE = 'New conversation';

@Component({
  selector: 'dm-conversation-list',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    RouterLink,
    TranslocoPipe,
    ErrorStateComponent,
    SkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './conversation-list.component.html',
  styleUrl: './conversation-list.component.scss',
})
export class ConversationListComponent {
  protected readonly store = inject(ConversationsStore);
  private readonly dialog = inject(MatDialog);
  private readonly confirmation = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly transloco = inject(TranslocoService);

  readonly activeId = input<string | null>(null);

  readonly selected = output<void>();
  readonly deleted = output<string>();

  protected readonly skeletonRows = [0, 1, 2, 3, 4, 5];

  protected displayTitle(conversation: ConversationSummary): string {
    return conversation.title === DEFAULT_TITLE
      ? this.transloco.translate('chat.defaultTitle')
      : conversation.title;
  }

  protected onSearch(event: Event): void {
    this.store.setQuery((event.target as HTMLInputElement).value);
  }

  protected clearSearch(): void {
    this.store.setQuery('');
  }

  protected async rename(conversation: ConversationSummary): Promise<void> {
    const reference = this.dialog.open<RenameDialogComponent, string, string>(
      RenameDialogComponent,
      { data: this.displayTitle(conversation), width: '26rem', maxWidth: '92vw' },
    );

    const title = await firstValueFrom(reference.afterClosed());
    if (title && title !== conversation.title) {
      await this.store.rename(conversation.id, title).catch(() => undefined);
    }
  }

  protected async remove(conversation: ConversationSummary): Promise<void> {
    const confirmed = await this.confirmation.confirm({
      titleKey: 'chat.delete.title',
      messageKey: 'chat.delete.message',
      confirmKey: 'chat.delete.confirm',
      params: { title: this.displayTitle(conversation) },
      destructive: true,
    });

    if (!confirmed) {
      return;
    }

    try {
      await this.store.remove(conversation.id);
      this.notifications.success('chat.delete.done');
      this.deleted.emit(conversation.id);
    } catch {
      // The HTTP error interceptor already reported the failure.
    }
  }
}
