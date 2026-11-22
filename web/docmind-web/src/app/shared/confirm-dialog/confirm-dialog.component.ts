import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';

export interface ConfirmOptions {
  readonly titleKey: string;
  readonly messageKey: string;
  readonly confirmKey: string;
  readonly params?: Record<string, unknown>;
  readonly destructive?: boolean;
}

@Component({
  selector: 'dm-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | transloco: data.params }}</h2>
    <mat-dialog-content>
      <p class="message">{{ data.messageKey | transloco: data.params }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">
        {{ 'common.cancel' | transloco }}
      </button>
      <button
        mat-flat-button
        type="button"
        cdkFocusInitial
        [class.destructive]="data.destructive"
        [mat-dialog-close]="true"
      >
        {{ data.confirmKey | transloco }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .message {
      margin: 0;
      overflow-wrap: anywhere;
    }

    .destructive {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmOptions>(MAT_DIALOG_DATA);
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  async confirm(options: ConfirmOptions): Promise<boolean> {
    const reference = this.dialog.open<ConfirmDialogComponent, ConfirmOptions, boolean>(
      ConfirmDialogComponent,
      { data: options, width: '26rem', maxWidth: '92vw', autoFocus: false },
    );

    return (await firstValueFrom(reference.afterClosed())) === true;
  }
}
