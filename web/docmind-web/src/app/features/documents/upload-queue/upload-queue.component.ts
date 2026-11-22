import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { BytesPipe } from '../../../shared/format/format.pipes';
import type { UploadItem } from '../state/documents.store';

@Component({
  selector: 'dm-upload-queue',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, TranslocoPipe, BytesPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './upload-queue.component.html',
  styleUrl: './upload-queue.component.scss',
})
export class UploadQueueComponent {
  readonly uploads = input.required<readonly UploadItem[]>();
  readonly canClear = input(false);

  readonly uploadCancelled = output<number>();
  readonly uploadDismissed = output<number>();
  readonly finishedCleared = output<void>();

  protected readonly language = toSignal(inject(TranslocoService).langChanges$, {
    initialValue: 'en',
  });

  protected isActive(upload: UploadItem): boolean {
    return upload.state === 'queued' || upload.state === 'uploading';
  }

  protected icon(upload: UploadItem): string {
    switch (upload.state) {
      case 'done':
        return 'check_circle';
      case 'failed':
        return 'error';
      case 'cancelled':
        return 'cancel';
      default:
        return 'upload_file';
    }
  }
}
