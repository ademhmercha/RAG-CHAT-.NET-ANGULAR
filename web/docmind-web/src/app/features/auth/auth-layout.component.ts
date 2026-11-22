import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PreferencesMenuComponent } from '../../layout/preferences/preferences-menu.component';
import { LogoComponent } from '../../shared/logo/logo.component';

@Component({
  selector: 'dm-auth-layout',
  imports: [RouterOutlet, LogoComponent, PreferencesMenuComponent, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="top">
        <dm-logo />
        <dm-preferences-menu />
      </header>

      <main class="center" id="main-content">
        <section class="card" [attr.aria-label]="'auth.formLabel' | transloco">
          <router-outlet />
        </section>
        <p class="tagline">{{ 'auth.tagline' | transloco }}</p>
      </main>
    </div>
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      min-height: 100dvh;
      background:
        radial-gradient(
          60rem 30rem at 50% -10%,
          color-mix(in srgb, var(--mat-sys-primary) 14%, transparent),
          transparent
        ),
        var(--mat-sys-surface);
    }

    .top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1rem 1.5rem;
    }

    .center {
      display: flex;
      flex: 1;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1.25rem;
      padding: 1rem;
    }

    .card {
      width: min(100%, 26rem);
      padding: 2rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dm-radius-card);
      background: var(--mat-sys-surface-container-lowest);
      box-shadow: var(--mat-sys-level2);
    }

    .tagline {
      margin: 0;
      max-width: 26rem;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
      text-align: center;
    }

    @media (max-width: 599.98px) {
      .card {
        padding: 1.5rem;
        box-shadow: none;
      }
    }
  `,
})
export class AuthLayoutComponent {}
