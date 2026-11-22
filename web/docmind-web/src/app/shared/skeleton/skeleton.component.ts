import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type SkeletonVariant = 'text' | 'circle' | 'block';

@Component({
  selector: 'dm-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[class]': 'variantClass()',
    '[style.width]': 'width()',
    '[style.height]': 'height()',
  },
  template: '',
  styles: `
    :host {
      display: block;
      background: linear-gradient(
        90deg,
        var(--mat-sys-surface-container-high) 25%,
        var(--mat-sys-surface-container-highest) 50%,
        var(--mat-sys-surface-container-high) 75%
      );
      background-size: 200% 100%;
      animation: dm-shimmer 1.4s ease-in-out infinite;
    }

    :host(.text) {
      height: 0.9rem;
      border-radius: 0.25rem;
    }

    :host(.circle) {
      border-radius: 50%;
    }

    :host(.block) {
      border-radius: var(--mat-sys-corner-medium);
    }

    @keyframes dm-shimmer {
      from {
        background-position: 200% 0;
      }
      to {
        background-position: -200% 0;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      :host {
        animation: none;
      }
    }
  `,
})
export class SkeletonComponent {
  readonly variant = input<SkeletonVariant>('text');
  readonly width = input('100%');
  readonly height = input<string | null>(null);

  protected readonly variantClass = computed(() => this.variant());
}
