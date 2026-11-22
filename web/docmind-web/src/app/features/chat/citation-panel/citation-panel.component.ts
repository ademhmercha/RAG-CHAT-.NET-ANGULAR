import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { Citation } from '../../../core/api/api-types';
import { BytesPipe } from '../../../shared/format/format.pipes';
import { SkeletonComponent } from '../../../shared/skeleton/skeleton.component';
import { DocumentsApi } from '../../documents/data/documents.api';

@Component({
  selector: 'dm-citation-panel',
  imports: [
    MatButtonModule,
    MatIconModule,
    RouterLink,
    TranslocoPipe,
    BytesPipe,
    SkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './citation-panel.component.html',
  styleUrl: './citation-panel.component.scss',
})
export class CitationPanelComponent {
  private readonly api = inject(DocumentsApi);

  readonly citation = input.required<Citation>();
  readonly index = input.required<number>();

  readonly closed = output<void>();

  protected readonly language = toSignal(inject(TranslocoService).langChanges$, {
    initialValue: 'en',
  });

  protected readonly source = rxResource({
    params: () => this.citation().documentId,
    stream: ({ params }) => (params ? this.api.get(params, true) : of(null)),
  });
}
