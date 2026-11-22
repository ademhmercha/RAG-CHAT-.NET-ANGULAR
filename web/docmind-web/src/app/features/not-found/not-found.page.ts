import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';

@Component({
  selector: 'dm-not-found-page',
  imports: [EmptyStateComponent, MatButtonModule, RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dm-empty-state
      icon="travel_explore"
      [title]="'notFound.title' | transloco"
      [description]="'notFound.description' | transloco"
    >
      <a mat-flat-button routerLink="/">{{ 'notFound.backHome' | transloco }}</a>
    </dm-empty-state>
  `,
})
export class NotFoundPage {}
