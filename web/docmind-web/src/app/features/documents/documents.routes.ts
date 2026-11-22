import { MatPaginatorIntl } from '@angular/material/paginator';
import type { Routes } from '@angular/router';
import { TranslatedPaginatorIntl } from '../../core/i18n/translated-paginator-intl';

export const DOCUMENTS_ROUTES: Routes = [
  {
    path: '',
    title: 'nav.documents',
    providers: [{ provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl }],
    loadComponent: () => import('./documents.page').then((m) => m.DocumentsPage),
  },
];
