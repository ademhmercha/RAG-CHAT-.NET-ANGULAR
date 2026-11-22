import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'dm-user-menu',
  imports: [
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    RouterLink,
    TranslocoPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="avatar"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="'layout.userMenu' | transloco"
    >
      {{ auth.initials() }}
    </button>
    <mat-menu #menu="matMenu" xPosition="before">
      <div class="identity" role="presentation">
        <strong>{{ auth.user()?.displayName }}</strong>
        <span>{{ auth.user()?.email }}</span>
      </div>
      <mat-divider />
      <a mat-menu-item routerLink="/settings">
        <mat-icon>settings</mat-icon>
        <span>{{ 'nav.settings' | transloco }}</span>
      </a>
      <button mat-menu-item type="button" (click)="signOut()">
        <mat-icon>logout</mat-icon>
        <span>{{ 'auth.signOut' | transloco }}</span>
      </button>
    </mat-menu>
  `,
  styles: `
    .avatar {
      display: grid;
      place-items: center;
      width: 2.25rem;
      height: 2.25rem;
      margin-left: 0.25rem;
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      font: var(--mat-sys-label-large);
      cursor: pointer;
    }

    .identity {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      padding: 0.75rem 1rem;
      max-width: 16rem;
    }

    .identity span {
      overflow: hidden;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      text-overflow: ellipsis;
    }
  `,
})
export class UserMenuComponent {
  protected readonly auth = inject(AuthStore);

  protected signOut(): void {
    void this.auth.logout();
  }
}
