import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'dm-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="logo">
      <svg class="mark" viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <rect width="32" height="32" rx="9" fill="var(--mat-sys-primary)" />
        <path
          d="M9 9.5h8.2a5.8 5.8 0 0 1 0 11.6H9z"
          fill="none"
          stroke="var(--mat-sys-on-primary)"
          stroke-width="2.4"
          stroke-linejoin="round"
        />
        <circle cx="21.5" cy="21.5" r="2.6" fill="var(--mat-sys-on-primary)" />
      </svg>
      @if (showName()) {
        <span class="name">DocMind</span>
      }
    </span>
  `,
  styles: `
    .logo {
      display: inline-flex;
      align-items: center;
      gap: 0.6rem;
    }

    .mark {
      width: 2rem;
      height: 2rem;
      flex: none;
    }

    .name {
      font: var(--mat-sys-title-large);
      font-weight: 650;
      letter-spacing: -0.01em;
    }
  `,
})
export class LogoComponent {
  readonly showName = input(true);
}
