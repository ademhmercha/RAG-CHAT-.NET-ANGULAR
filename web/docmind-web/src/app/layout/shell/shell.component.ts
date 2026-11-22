import { BreakpointObserver } from '@angular/cdk/layout';
import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import { LogoComponent } from '../../shared/logo/logo.component';
import { PreferencesMenuComponent } from '../preferences/preferences-menu.component';
import { UserMenuComponent } from '../user-menu/user-menu.component';

interface NavItem {
  readonly path: string;
  readonly icon: string;
  readonly labelKey: string;
}

const HANDSET_QUERY = '(max-width: 959.98px)';

@Component({
  selector: 'dm-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    TranslocoPipe,
    LogoComponent,
    PreferencesMenuComponent,
    UserMenuComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  protected readonly navItems: readonly NavItem[] = [
    { path: '/chat', icon: 'chat_bubble_outline', labelKey: 'nav.chat' },
    { path: '/documents', icon: 'description', labelKey: 'nav.documents' },
    { path: '/settings', icon: 'settings', labelKey: 'nav.settings' },
  ];

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe(HANDSET_QUERY)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly drawerOpen = signal(false);
  protected readonly sidenavOpened = computed(() => !this.isHandset() || this.drawerOpen());

  constructor() {
    let section: string | null = null;
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.drawerOpen.set(false);

        const next = event.urlAfterRedirects.split(/[/?#]/).find(Boolean) ?? '';
        if (section !== null && next !== section) {
          this.document.getElementById('main-content')?.focus({ preventScroll: true });
        }
        section = next;
      });

    effect(() => {
      if (!this.isHandset()) {
        this.drawerOpen.set(false);
      }
    });
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected onDrawerClosed(): void {
    this.drawerOpen.set(false);
  }
}
