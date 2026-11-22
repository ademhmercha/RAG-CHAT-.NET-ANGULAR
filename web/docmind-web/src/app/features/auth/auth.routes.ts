import type { Routes } from '@angular/router';
import { AuthLayoutComponent } from './auth-layout.component';

export const AUTH_ROUTES: Routes = [
  {
    path: '',
    component: AuthLayoutComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'login' },
      {
        path: 'login',
        title: 'auth.login.title',
        loadComponent: () => import('./login.page').then((m) => m.LoginPage),
      },
      {
        path: 'register',
        title: 'auth.register.title',
        loadComponent: () => import('./register.page').then((m) => m.RegisterPage),
      },
    ],
  },
];
