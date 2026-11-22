import type { OnInit } from '@angular/core';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule } from '@angular/material/paginator';
import type { PageEvent } from '@angular/material/paginator';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { DocumentItem, DocumentStatus } from '../../core/api/api-types';
import { DOCUMENT_STATUSES } from '../../core/api/api-types';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/error-state/error-state.component';
import { BytesPipe, DateTimePipe } from '../../shared/format/format.pipes';
import { SkeletonComponent } from '../../shared/skeleton/skeleton.component';
import { DropzoneComponent } from './dropzone/dropzone.component';
import { DocumentsStore } from './state/documents.store';
import { StatusChipComponent } from './status-chip/status-chip.component';
import { UploadQueueComponent } from './upload-queue/upload-queue.component';

const PAGE_SIZE_OPTIONS = [10, 20, 50];

@Component({
  selector: 'dm-documents-page',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatPaginatorModule,
    TranslocoPipe,
    BytesPipe,
    DateTimePipe,
    DropzoneComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    SkeletonComponent,
    StatusChipComponent,
    UploadQueueComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './documents.page.html',
  styleUrl: './documents.page.scss',
})
export class DocumentsPage implements OnInit {
  protected readonly store = inject(DocumentsStore);
  private readonly confirmation = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly language = toSignal(inject(TranslocoService).langChanges$, {
    initialValue: 'en',
  });

  protected readonly statuses = DOCUMENT_STATUSES;
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly skeletonRows = [0, 1, 2, 3, 4];
  protected readonly filterValue = computed(() => this.store.status() ?? 'all');

  constructor() {
    inject(DestroyRef).onDestroy(() => this.store.stopLive());
  }

  ngOnInit(): void {
    void this.store.load();
    this.store.startLive();
  }

  protected onFilter(value: DocumentStatus | 'all'): void {
    void this.store.setStatus(value === 'all' ? null : value);
  }

  protected onPage(event: PageEvent): void {
    void this.store.setPage(event.pageIndex + 1, event.pageSize);
  }

  protected async remove(document: DocumentItem): Promise<void> {
    const confirmed = await this.confirmation.confirm({
      titleKey: 'documents.delete.title',
      messageKey: 'documents.delete.message',
      confirmKey: 'documents.delete.confirm',
      params: { name: document.fileName },
      destructive: true,
    });

    if (!confirmed) {
      return;
    }

    try {
      await this.store.remove(document.id);
      this.notifications.success('documents.delete.done');
    } catch {
      // The HTTP error interceptor already reported the failure.
    }
  }
}
