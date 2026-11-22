import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'dm-empty-state',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty" role="status">
      <div class="icon-wrap" aria-hidden="true">
        <mat-icon>{{ icon() }}</mat-icon>
      </div>
      <h2 class="title">{{ title() }}</h2>
      @if (description()) {
        <p class="description">{{ description() }}</p>
      }
      <div class="actions">
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 3rem 1.5rem;
      text-align: center;
    }

    .icon-wrap {
      display: grid;
      place-items: center;
      width: 4rem;
      height: 4rem;
      margin-bottom: 0.5rem;
      border-radius: 50%;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }

    .icon-wrap mat-icon {
      width: 2rem;
      height: 2rem;
      font-size: 2rem;
    }

    .title {
      margin: 0;
      font: var(--mat-sys-title-large);
    }

    .description {
      max-width: 34rem;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-large);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 0.5rem;
      margin-top: 1rem;
    }

    .actions:empty {
      display: none;
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly description = input<string>('');
}
