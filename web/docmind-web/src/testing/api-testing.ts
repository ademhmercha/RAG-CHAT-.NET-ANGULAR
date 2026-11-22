import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import type { EnvironmentProviders, Provider } from '@angular/core';
import { APP_CONFIG } from '../app/core/config/app-config';

export function provideApiTesting(): (Provider | EnvironmentProviders)[] {
  return [
    { provide: APP_CONFIG, useValue: { apiBaseUrl: '', environment: 'test' } },
    provideHttpClient(),
    provideHttpClientTesting(),
  ];
}
