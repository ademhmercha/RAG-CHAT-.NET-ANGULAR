import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { DocumentScopeStore } from '../state/document-scope.store';

@Component({
  selector: 'dm-scope-selector',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      mat-stroked-button
      type="button"
      class="trigger"
      data-testid="scope-trigger"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="'chat.scope.title' | transloco"
      (menuOpened)="scope.refresh()"
    >
      <mat-icon>filter_list</mat-icon>
      @if (scope.selectedCount() > 0) {
        {{ 'chat.scope.selected' | transloco: { count: scope.selectedCount() } }}
      } @else {
        {{ 'chat.scope.all' | transloco }}
      }
    </button>

    <mat-menu #menu="matMenu" class="dm-scope-menu" xPosition="after" yPosition="above">
      <div class="menu-title" role="presentation">{{ 'chat.scope.title' | transloco }}</div>

      @if (scope.selectedCount() > 0) {
        <button mat-menu-item type="button" (click)="scope.clear(); $event.stopPropagation()">
          <mat-icon>layers_clear</mat-icon>
          <span>{{ 'chat.scope.clear' | transloco }}</span>
        </button>
      }

      @for (document of scope.documents(); track document.id) {
        <button
          mat-menu-item
          type="button"
          role="menuitemcheckbox"
          [attr.aria-checked]="scope.selectedIds().includes(document.id)"
          (click)="scope.toggle(document.id); $event.stopPropagation()"
        >
          <mat-icon>
            {{
              scope.selectedIds().includes(document.id) ? 'check_box' : 'check_box_outline_blank'
            }}
          </mat-icon>
          <span class="document-name">{{ document.fileName }}</span>
        </button>
      } @empty {
        @if (!scope.loading()) {
          <p class="none">{{ 'chat.scope.none' | transloco }}</p>
          <a mat-menu-item routerLink="/documents">
            <mat-icon>upload_file</mat-icon>
            <span>{{ 'chat.scope.upload' | transloco }}</span>
          </a>
        }
      }
    </mat-menu>
  `,
  styles: `
    :host {
      display: inline-block;
    }

    .trigger {
      max-width: 100%;
    }

    .menu-title {
      padding: 0.5rem 1rem;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }

    .none {
      max-width: 16rem;
      margin: 0;
      padding: 0.5rem 1rem;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }

    .document-name {
      display: inline-block;
      max-width: 16rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: middle;
    }
  `,
})
export class ScopeSelectorComponent {
  protected readonly scope = inject(DocumentScopeStore);

  constructor() {
    void this.scope.ensureLoaded();
  }
}
