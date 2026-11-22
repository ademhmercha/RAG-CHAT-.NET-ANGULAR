import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import type { DocumentStatus } from '../../../core/api/api-types';

const ICONS: Readonly<Record<DocumentStatus, string>> = {
  Pending: 'schedule',
  Processing: 'sync',
  Ready: 'check_circle',
  Failed: 'error',
};

@Component({
  selector: 'dm-status-chip',
  imports: [MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'status()' },
  template: `
    <mat-icon aria-hidden="true" [class.spin]="status() === 'Processing'">{{ icon() }}</mat-icon>
    {{ 'documents.status.' + status() | transloco }}
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.2rem 0.65rem 0.2rem 0.4rem;
      border-radius: var(--mat-sys-corner-full);
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
    }

    mat-icon {
      width: 1.1rem;
      height: 1.1rem;
      font-size: 1.1rem;
    }

    :host(.Pending) {
      background: var(--mat-sys-surface-container-highest);
      color: var(--mat-sys-on-surface-variant);
    }

    :host(.Processing) {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    :host(.Ready) {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }

    :host(.Failed) {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }

    .spin {
      animation: dm-spin 1.2s linear infinite;
    }

    @keyframes dm-spin {
      to {
        transform: rotate(360deg);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .spin {
        animation: none;
      }
    }
  `,
})
export class StatusChipComponent {
  readonly status = input.required<DocumentStatus>();

  protected readonly icon = computed(() => ICONS[this.status()]);
}
