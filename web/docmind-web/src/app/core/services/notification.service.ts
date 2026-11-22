import { Injectable, inject } from '@angular/core';
import type { MatSnackBarConfig } from '@angular/material/snack-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';

const DEFAULT_DURATION_MS = 5000;
const ERROR_DURATION_MS = 8000;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);
  private readonly transloco = inject(TranslocoService);

  success(messageKey: string, params?: Record<string, unknown>): void {
    this.show(
      this.transloco.translate(messageKey, params),
      'dm-snackbar-success',
      DEFAULT_DURATION_MS,
    );
  }

  info(messageKey: string, params?: Record<string, unknown>): void {
    this.show(
      this.transloco.translate(messageKey, params),
      'dm-snackbar-info',
      DEFAULT_DURATION_MS,
    );
  }

  error(message: string): void {
    this.show(message, 'dm-snackbar-error', ERROR_DURATION_MS);
  }

  private show(message: string, panelClass: string, duration: number): void {
    const config: MatSnackBarConfig = {
      duration,
      panelClass,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
    };

    this.snackBar.open(message, this.transloco.translate('common.dismiss'), config);
  }
}
