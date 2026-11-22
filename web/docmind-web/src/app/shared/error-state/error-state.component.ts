import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'dm-error-state',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="error" role="alert">
      <mat-icon class="icon" aria-hidden="true">error_outline</mat-icon>
      <h2 class="title">{{ title() || ('errors.generic.title' | transloco) }}</h2>
      <p class="message">{{ message() }}</p>
      @if (retryable()) {
        <button mat-tonal-button type="button" (click)="retry.emit()">
          <mat-icon>refresh</mat-icon>
          {{ 'common.retry' | transloco }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 2.5rem 1.5rem;
      text-align: center;
    }

    .icon {
      width: 3rem;
      height: 3rem;
      font-size: 3rem;
      color: var(--mat-sys-error);
    }

    .title {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }

    .message {
      max-width: 32rem;
      margin: 0 0 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ErrorStateComponent {
  readonly title = input('');
  readonly message = input.required<string>();
  readonly retryable = input(true);
  readonly retry = output<void>();
}
