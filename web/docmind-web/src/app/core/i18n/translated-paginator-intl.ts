import { Injectable, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';

@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly transloco = inject(TranslocoService);

  constructor() {
    super();
    this.transloco.langChanges$.subscribe(() => this.refresh());
  }

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return this.transloco.translate('common.pagination.rangeEmpty', { total: length });
    }

    const start = page * pageSize;
    return this.transloco.translate('common.pagination.range', {
      start: start + 1,
      end: Math.min(start + pageSize, length),
      total: length,
    });
  };

  private refresh(): void {
    this.itemsPerPageLabel = this.transloco.translate('common.pagination.itemsPerPage');
    this.nextPageLabel = this.transloco.translate('common.pagination.nextPage');
    this.previousPageLabel = this.transloco.translate('common.pagination.previousPage');
    this.firstPageLabel = this.transloco.translate('common.pagination.firstPage');
    this.lastPageLabel = this.transloco.translate('common.pagination.lastPage');
    this.changes.next();
  }
}
