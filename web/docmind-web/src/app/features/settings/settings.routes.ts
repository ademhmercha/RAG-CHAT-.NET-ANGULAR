import type { Routes } from '@angular/router';

export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    title: 'nav.settings',
    loadComponent: () => import('./settings.page').then((m) => m.SettingsPage),
  },
];
